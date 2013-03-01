/*   Builds an X-Ray file to be used on the Amazon Kindle
*   Original xray builder by shinew, http://www.mobileread.com/forums/showthread.php?t=157770 , http://www.xunwang.me/xray/
*   
*   Copyright (C) 2013 Ephemerality <Nick Niemi - ephemeral.vilification@gmail.com>
*
*   This program is free software: you can redistribute it and/or modify
*   it under the terms of the GNU General Public License as published by
*   the Free Software Foundation, either version 3 of the License, or
*   (at your option) any later version.

*   This program is distributed in the hope that it will be useful,
*   but WITHOUT ANY WARRANTY; without even the implied warranty of
*   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*   GNU General Public License for more details.

*   You should have received a copy of the GNU General Public License
*   along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

// HTMLAgilityPack from http://htmlagilitypack.codeplex.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace XRayBuilder
{
    class XRay
    {
        string shelfariURL = "";
        string databaseName = "";
        string guid = "";
        string asin = "";
        string version = "1";
        List<Term> terms = new List<Term>(100);
        List<Chapter> chapters = new List<Chapter>();
        long srl = 0;
        long erl = 0;
        bool shortEx = true;
        bool useSpoilers = false;
        int locOffset = 0;

        public XRay()
        {
        }

        public XRay(string shelfari, string db, string guid, string asin, bool useSpoilers = false, int locOffset = 0)
        {
            if (shelfari == "" || db == "" || guid == "" || asin == "")
                throw new ArgumentException("Error initializing X-Ray, one of the required parameters was blank.");
                
            this.shelfariURL = shelfari;
            this.databaseName = db;
            this.guid = guid;
            this.asin = asin;
            this.useSpoilers = useSpoilers;
            this.locOffset = locOffset;
        }

        public override string ToString()
        {
            //Insert a version tag of the current program version so you know which version built it.
            //Will be ignored by the Kindle.
            Version dd = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string xrayversion = dd.Major.ToString() + "." + dd.Minor.ToString() + dd.Build.ToString();
            //If there are no chapters built (someone only ran createXRAY), just use the default version
            if (chapters.Count > 0)
                return String.Format(@"{{""asin"":""{0}"",""guid"":""{1}:{2}"",""version"":""{3}"",""xrayversion"":""{8}"",""terms"":[{4}],""chapters"":[{5}],""assets"":{{}},""srl"":{6},""erl"":{7}}}",
                    asin, databaseName, guid, version, string.Join<Term>(",", terms), string.Join<Chapter>(",", chapters), srl, erl, xrayversion);
            else
            {
                return String.Format(@"{{""asin"":""{0}"",""guid"":""{1}:{2}"",""version"":""{3}"",""xrayversion"":""{5}"",""terms"":[{4}],""chapters"":[{{""name"":null,""start"":1,""end"":9999999}}]}}",
                    asin, databaseName, guid, version, string.Join<Term>(",", terms), xrayversion);
            }
        }

        public string getXRayName()
        {
            return "XRAY.entities." + asin + ".asc";
        }

        public int createXRAY()
        {

            //Process GUID. If in decimal form, convert to hex.
            if (Regex.IsMatch(guid, "/[a-zA-Z]/"))
                guid = guid.ToUpper();
            else
            {
                long guidDec;
                long.TryParse(guid, out guidDec);
                guid = guidDec.ToString("X");
            }
            if (guid == "0")
            {
                Console.WriteLine("Something bad happened while converting the GUID.");
                return 1;
            }

            Console.WriteLine("Downloading Shelfari page... {0}", useSpoilers ? "SHOWING SPOILERS!" : "");
            //Download HTML of Shelfari URL, try 3 times just in case it fails the first time
            string shelfariHTML = "";
            int tries = 3;
            do
            {
                try
                {
                    //Enable cookies on extended webclient
                    CookieContainer jar = new CookieContainer();
                    using (WebClientEx client = new WebClientEx(jar))
                    {
                        //shelfariURL = "http://www.shelfari.com/books/25411/The-Path-of-Daggers";
                        if (useSpoilers)
                        {
                            //Grab book ID from url (search for 5 digits between slashes) and create spoiler cookie
                            string bookID = Regex.Match(shelfariURL, @"\/\d{5}").Value.Substring(1, 5);
                            Cookie spoilers = new Cookie("ShelfariBookWikiSession", "", "/", "www.shelfari.com");
                            spoilers.Value = "{\"SpoilerShowAll\":true%2C\"SpoilerShowCharacters\":true%2C\"SpoilerBookId\":" + bookID + "%2C\"SpoilerShowPSS\":true%2C\"SpoilerShowQuotations\":true%2C\"SpoilerShowParents\":true%2C\"SpoilerShowThemes\":true}";
                            jar.Add(spoilers);
                        }
                        shelfariHTML = client.DownloadString(shelfariURL);
                        break;
                    }
                }
                catch
                {
                    if (tries <= 0)
                    {
                        Console.WriteLine("Failed to connect to Shelfari URL.");
                        return 1;
                    }
                }
            } while (tries-- > 0);

            /*** Constants for wiki processing ***/
            Dictionary<string, string> sections = new Dictionary<string, string>{
                {"WikiModule_Characters", "character"}, {"WikiModule_Organizations", "topic"}, {"WikiModule_Settings", "topic"},
                {"WikiModule_Glossary", "topic"} }; //, {"WikiModule_Themes", "topic"} };
            string[] patterns = { @"""", @"\[\d\]", @"\s*?\(.*\)\s*?" }; //Escape quotes, numbers in brackets, and anything within brackets at all
            string[] replacements = { @"\""", @"", @"" };
            /************************************/

            //Parse elements from various headers listed in sections
            HtmlAgilityPack.HtmlDocument shelfariDoc = new HtmlAgilityPack.HtmlDocument();
            shelfariDoc.LoadHtml(shelfariHTML);
            foreach (string header in sections.Keys)
            {
                if (!shelfariHTML.Contains(header)) continue; //Skip section if not found on page
                //Select <li> nodes on page from within the <div id=header> tag, under <ul class=li_6>
                HtmlNodeCollection characterNodes = shelfariDoc.DocumentNode.SelectNodes("//div[@id='" + header + "']//ul[@class='li_6']/li");
                foreach (HtmlNode li in characterNodes)
                {
                    string tmpString = li.InnerText;
                    Term newTerm = new Term(sections[header]); //Create term as either character/topic
                    if (tmpString.Contains(":"))
                    {
                        newTerm.termName = tmpString.Substring(0, tmpString.IndexOf(":"));
                        newTerm.desc = tmpString.Substring(tmpString.IndexOf(":") + 1).Trim();
                    }
                    else
                    {
                        newTerm.termName = tmpString;
                    }
                    newTerm.termName = newTerm.termName.PregReplace(patterns, replacements);
                    newTerm.desc = newTerm.desc.PregReplace(patterns, replacements);
                    newTerm.descSrc = "shelfari";
                    //Use either the associated shelfari URL of the term or if none exists, use the book's url
                    //Could use a wikipedia page instead as the xray plugin/site does but I decided not to
                    newTerm.descUrl = (li.InnerHtml.IndexOf("<a href") == 0 ? li.InnerHtml.Substring(9, li.InnerHtml.IndexOf("\"", 9) - 9) : shelfariURL);
                    if (header == "WikiModule_Glossary") newTerm.matchCase = false; //Default glossary terms to be case insensitive when searching through book
                    terms.Add(newTerm);
                }
            }

            //Export list of Shelfari characters to a file to make it easier to create aliases or import the modified aliases if they exist
            //Could potentially just attempt to automate the creation of aliases, but in some cases it is very subjective...
            //For example, Shelfari shows the character "Artemis Fowl II", but in the book he is either referred to as "Artemis Fowl", "Artemis", or even "Arty"
            //Other characters have one name on Shelfari but can have completely different names within the book
            string aliasFile = Environment.CurrentDirectory + "\\ext\\" + asin + ".aliases";
            if (!File.Exists(aliasFile))
            {
                saveCharacters();
                Console.WriteLine("Characters exported to {0} for adding aliases.", aliasFile);
            }
            else
            {
                loadAliases();
                Console.WriteLine("Character aliases read from {0}.", aliasFile);
            }

            Console.WriteLine("Terms found on Shelfari:");
            foreach (Term t in terms)
                Console.Write("{0}, ", t.termName);
            Console.Write("\b\b  \nContinue building using these terms (cancel if you want to add aliases)? (Y/N) ");
            string input = Console.ReadLine();
            if (input.ToLower() != "y")
                return 1;

            return 0;
        }

        public int expandFromRawML(string rawML)
        {
            HtmlDocument web = new HtmlAgilityPack.HtmlDocument();

            string readContents;
            using (StreamReader streamReader = new StreamReader(rawML, Encoding.Default))
            {
                readContents = streamReader.ReadToEnd();
            }
            web.LoadHtml(readContents);
            //if (web.ParseErrors != null && web.ParseErrors.Count() > 0)
            //    web = web;
            //Similar to aliases, if chapters definition exists, load it. Otherwise, attempt to build it from the book
            string chapterFile = Environment.CurrentDirectory + "\\ext\\" + asin + ".chapters";
            if (File.Exists(chapterFile))
            {
                loadChapters();
                Console.WriteLine("Chapters read from {0}.\nDelete this file if you want chapters built automatically.", chapterFile);
            }
            else
            {
                string leadingZeros = @"^0+(?=\d)";
                chapters.Clear();
                //Find table of contents, using case-insensitive search
                HtmlNode toc = web.DocumentNode.SelectSingleNode("//reference[translate(@title,'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ')='TABLE OF CONTENTS']");
                if (toc != null)
                {
                    int tocloc = Convert.ToInt32(Regex.Replace(toc.GetAttributeValue("filepos", ""), leadingZeros, ""));
                    //string tochtml = readContents.Substring(readContents.IndexOf("<p", tocloc), readContents.IndexOf("<mbp:pagebreak/>", tocloc + 1) - tocloc);
                    string tochtml = readContents.Substring(tocloc, readContents.IndexOf("<mbp:pagebreak/>", tocloc + 1) - tocloc);
                    HtmlDocument tocdoc = new HtmlDocument();
                    tocdoc.LoadHtml(tochtml);
                    HtmlNodeCollection tocnodes = tocdoc.DocumentNode.SelectNodes("//a");
                    foreach (HtmlNode chapter in tocnodes)
                    {
                        int filepos = Convert.ToInt32(Regex.Replace(chapter.GetAttributeValue("filepos", "0"), leadingZeros, ""));
                        if (filepos == 0)
                            filepos = 0;
                        if (chapters.Count > 0)
                        {
                            chapters[chapters.Count - 1].end = filepos;
                            if (chapters[chapters.Count - 1].start > filepos) chapters.RemoveAt(chapters.Count - 1); //remove broken chapters
                        }
                        chapters.Add(new Chapter(chapter.InnerText, filepos, readContents.Length));
                    }
                }
                //Built chapters list is saved for manual editing
                if (chapters.Count > 0)
                {
                    saveChapters();
                    Console.WriteLine("Chapters exported to {0} for manual editing.", chapterFile);
                }
                else
                    Console.WriteLine("No chapters detected. You can create a file at {0} if you want to define chapters manually.", chapterFile);
            }

            //If no chapters were found, add a default chapter that spans the entire book
            //Define srl and erl so "progress bar" shows up correctly
            if (chapters.Count == 0)
            {
                long len = (new FileInfo(rawML)).Length;
                chapters.Add(new Chapter("", 1, len));
                srl = 1;
                erl = len;
            } else {
                //Run through all chapters and take the highest value, in case some chapters can be defined in individual chapters and parts.
                //IE. Part 1 includes chapters 1-6, Part 2 includes chapters 7-12.
                srl = chapters[0].start;
                Console.WriteLine("Found chapters:");
                foreach (Chapter c in chapters)
                {
                    if (c.end > erl) erl = c.end;
                    Console.WriteLine(c.name);
                }
    
                Console.Write("\b\b  \nContinue building using these chapters? (Y/N) ");
                string input = Console.ReadLine();
                if (input.ToLower() != "y")
                    return 1;
            }

            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            //Iterate over all paragraphs in book
            HtmlNodeCollection nodes = web.DocumentNode.SelectNodes("//p");
            for (int i = 0; i < nodes.Count; i++)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write("Scanning book content: {0}          ", ((double)(i + 1) / nodes.Count).ToString("##.0%"));

                HtmlNode node = nodes[i];
                int lenQuote = node.InnerHtml.Length;
                int location = node.FirstChild.StreamPosition;
                if (location < 0)
                {
                    Console.WriteLine("There was a serious error locating the paragraph within the book content.");
                    return 1;
                }
                if (location < srl || location > erl) continue; //Skip paragraph if outside chapter range
                foreach (Term character in terms)
                {
                    //Search for character name and aliases in either the html-less text. If failed, try in the HTML for rare situations.
                    //TODO: Improve location searching, as IndexOf will not work if book length exceeds 2,147,483,647...
                    List<string> search = character.aliases.ToList<string>();
                    search.Insert(0, character.termName);
                    if ((character.matchCase && (search.Any(node.InnerText.Contains) || search.Any(node.InnerHtml.Contains))) ||
                        (!character.matchCase && (search.Any(node.InnerText.ContainsIgnorecase) || search.Any(node.InnerHtml.ContainsIgnorecase))))
                    {
                        int locHighlight = -1;
                        int lenHighlight = -1;
                        //Search html for the matching term out of all aliases
                        foreach (string s in search)
                        {
                            int index = node.InnerHtml.IndexOf(s, character.matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                            if (index >= 0)
                            {
                                locHighlight = index;
                                lenHighlight = s.Length;
                                break;
                            }
                        }
                        //If normal search fails, use regexp to search in case there is some wacky html nested in term
                        //Regexp may be less than ideal for parsing HTML but seems to work ok so far in these small paragraphs
                        if (locHighlight < 0)
                        {
                            foreach (string s in search)
                            {
                                string pattern = "(?:<[^>]*>)*"; //Match HTML tags -- provided there's nothing malformed
                                pattern = string.Format("{0}{1}{0}(?=\\W)", pattern, string.Join(pattern, Regex.Unescape(s).ToCharArray()));
                                Match match = Regex.Match(node.InnerHtml, pattern);
                                if (match.Success)
                                {
                                    locHighlight = match.Index;
                                    lenHighlight = match.Length;
                                    break;
                                }
                            }
                        }
                        if (locHighlight < 0) //something went wrong
                        {
                            Console.WriteLine("\nSomething went wrong while searching for start of highlight.\nWas looking for (or one of the aliases of): {0}\nSearching in: {1}", character.termName, node.InnerHtml);
                            continue;
                        }

                        /*****
                         * If an excerpt is too long, the X-Ray reader cuts it off.
                         * If the location of the highlighted word (character name) within the excerpt is far enough in to get cut off,
                         * this section attempts to shorted the excerpt by locating the start of a sentence that is just far enough away from the highlight.
                         * The length is determined by the space the excerpt takes up rather than its length... so 135 is just a guess based on what I've seen.
                         * Not fully tested yet
                         * TODO: Clean up
                         *****/
                        int lengthLimit = 135;
                        if (shortEx && locHighlight + lenHighlight > lengthLimit)
                        {
                            int start = locHighlight;
                            int at = 0;
                            long newLoc = -1;
                            int newLenQuote = 0;
                            int newLocHighlight = 0;

                            while ((start > -1) && (at > -1))
                            {
                                at = node.InnerHtml.LastIndexOf(". ", start); //Any(new char[] { '.', '?', '!' }, start, start);
                                //at += Regex.Match(node.InnerHtml.Substring(at + 1), "\\S").Index;
                                if (at > -1)
                                {
                                    start = at - 1;

                                    if ((locHighlight + lenHighlight + 1 - at - 2) <= lengthLimit)
                                    {
                                        newLoc = location + at + 2;
                                        newLenQuote = lenQuote - at - 2;
                                        newLocHighlight = locHighlight - at - 2;
                                        string newQuote = node.InnerHtml.Substring(at + 2);
                                    }
                                    else break;
                                }
                                else break;
                            }
                            //Only add new locs if shorter excerpt was found
                            if (newLoc >= 0)
                            {
                                character.locs.Add(String.Format("[{0},{1},{2},{3}]", newLoc + locOffset, newLenQuote, newLocHighlight, lenHighlight));
                                continue;
                            }
                        }

                        character.locs.Add(String.Format("[{0},{1},{2},{3}]", location + locOffset, lenQuote, locHighlight, lenHighlight));

                        //Console.WriteLine(node.OuterHtml);
                    }
                }
            }
            timer.Stop();
            Console.WriteLine("\nCreation time: {0}", timer.Elapsed);
            Console.WriteLine("");
            return 0;
        }

        class Chapter
        {
            public string name;
            public long start;
            public long end;

            public Chapter()
            {
                this.name = "";
                this.start = 1;
                this.end = 9999999;
            }

            public Chapter(string name, long start, long end)
            {
                this.name = name;
                this.start = start;
                this.end = end;
            }

            public override string ToString()
            {
                return String.Format(@"{{""name"":{0},""start"":{1},""end"":{2}}}", (name == "" ? "null" : "\"" + name + "\""), start, end);
            }
        }

        class Term
        {
            public string type = "";
            public string termName = "";
            public string desc = "";
            public string descSrc = "";
            public string descUrl = "";
            public List<string> aliases = new List<string>();
            public List<string> locs = new List<string>(1000);
            public List<string> assets = new List<string> { "" };
            public bool matchCase = true;

            public Term() { }

            public Term(string type)
            {
                this.type = type;
            }

            public override string ToString()
            {
                //Note that the Amazon X-Ray files declare an "assets" var for each term, but I have not seen one that actually uses them
                if (locs.Count > 0)
                    return String.Format(@"{{""type"":""{0}"",""term"":""{1}"",""desc"":""{2}"",""descSrc"":""{3}"",""descUrl"":""{4}"",""locs"":[{5}]}}", //,""assets"":[{6}]}}",
                        type, termName, desc, descSrc, descUrl, string.Join(",", locs));
                else
                    return String.Format(@"{{""type"":""{0}"",""term"":""{1}"",""desc"":""{2}"",""descSrc"":""{3}"",""descUrl"":""{4}"",""locs"":[[100,100,100,6]]}}", //,""assets"":[{6}]}}",
                        type, termName, desc, descSrc, descUrl);
            }
        }

        public void saveChapters()
        {
            if (!Directory.Exists(Environment.CurrentDirectory + "\\ext\\")) Directory.CreateDirectory(Environment.CurrentDirectory + "\\ext\\");
            using (StreamWriter streamWriter = new StreamWriter(Environment.CurrentDirectory + "\\ext\\" + asin + ".chapters", false))
            {
                foreach (Chapter c in chapters)
                    streamWriter.WriteLine(c.name + "|" + c.start + "|" + c.end);
            }
        }

        public void loadChapters()
        {
            chapters = new List<Chapter>();
            using (StreamReader streamReader = new StreamReader(Environment.CurrentDirectory + "\\ext\\" + asin + ".chapters"))
            {
                while (!streamReader.EndOfStream)
                {
                    string[] tmp = streamReader.ReadLine().Split('|');
                    if (tmp[0].Substring(0, 1) == "#") continue;
                    chapters.Add(new Chapter(tmp[0], Convert.ToInt32(tmp[1]), Convert.ToInt64(tmp[2])));
                }
            }
        }

        public void saveCharacters()
        {
            if (!Directory.Exists(Environment.CurrentDirectory + "\\ext\\")) Directory.CreateDirectory(Environment.CurrentDirectory + "\\ext\\");
            using (StreamWriter streamWriter = new StreamWriter(Environment.CurrentDirectory + "\\ext\\" + asin + ".aliases", false))
            {
                foreach (Term c in terms)// if(c.type == "character")
                    streamWriter.WriteLine(c.termName + "|");
            }
        }

        public void loadAliases()
        {
            Dictionary<string, string[]> d = new Dictionary<string, string[]>();
            using (StreamReader streamReader = new StreamReader(Environment.CurrentDirectory + "\\ext\\" + asin + ".aliases"))
            {
                while (!streamReader.EndOfStream)
                {
                    string[] temp = streamReader.ReadLine().Split('|');
                    if (temp[0].Substring(0, 1) == "#") continue;
                    if (temp.Length <= 1) continue;
                    string[] temp2 = temp[1].Split(',');
                    if (temp2.Length == 0 || temp2[0] == "") continue;
                    d.Add(temp[0], temp2);
                }
            }
            foreach (Term t in terms)
            {
                if (d.ContainsKey(t.termName))
                    t.aliases = new List<string>(d[t.termName]);
            }
        }
    }

    public static class ExtensionMethods
    {
        //http://stackoverflow.com/questions/166855/c-sharp-preg-replace
        public static String PregReplace(this String input, string[] pattern, string[] replacements)
        {
            if (replacements.Length != pattern.Length)
                throw new ArgumentException("Replacement and Pattern Arrays must be balanced");

            for (var i = 0; i < pattern.Length; i++)
            {
                bool s = Regex.IsMatch(input, "\"");
                input = Regex.Replace(input, pattern[i], replacements[i]);
            }

            return input;
        }

        //http://stackoverflow.com/questions/444798/case-insensitive-containsstring
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }

        public static bool ContainsIgnorecase(this string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    //Taken from http://stackoverflow.com/questions/1777221/using-cookiecontainer-with-webclient-class
    //To avoid using HttpWebRequest directly!
    [System.ComponentModel.DesignerCategory("")]
    public class WebClientEx : WebClient
    {
        public WebClientEx(CookieContainer container)
        {
            this.container = container;
        }

        private readonly CookieContainer container = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);
            var request = r as HttpWebRequest;
            if (request != null)
            {
                request.CookieContainer = container;
            }
            return r;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            WebResponse response = base.GetWebResponse(request, result);
            ReadCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            ReadCookies(response);
            return response;
        }

        private void ReadCookies(WebResponse r)
        {
            var response = r as HttpWebResponse;
            if (response != null)
            {
                CookieCollection cookies = response.Cookies;
                container.Add(cookies);
            }
        }
    }
}
