*** X-Ray Builder ****
* A console application that takes a mobi book file and creates an X-Ray file for that book with * excerpt locations and chapter definitions.
*
* Created by Ephemerality <Nick Niemi - ephemeral.vilification@gmail.com>
* Original X-Ray script by shinew
* (http://www.mobileread.com/forums/showthread.php?t=157770)
* (http://www.xunwang.me/xray/)
**********************

Requirements:
-mobi2mobi (https://dev.mobileread.com/dist/tompe/mobiperl/)
-mobi_unpack (http://www.mobileread.com/forums/attachment.php?attachmentid=84428&d=1332545649)
-HtmlAgilityPack (http://htmlagilitypack.codeplex.com/)
 I have included the .dll along with the source and binaries. As far as I can tell, this is  allowed. If not, let me know!

Program usage:
xraybuilder [-m path] [-o path] [-r] [-s shelfariURL] [-u path] mobiPath\n" +

-m path (--mobi2mobi)	Path must point to mobi2mobi.exe
			If not specified, searches in the current directory
-o path (--outdir)	Path defines the output directory
			If not specified, uses ./out
-r (--saveraw)		Save raw book markup to the output directory
-s (--shelfari)		Shelfari URL
			If not specified, there will be a prompt asking for it
-u path (--unpack)	Path must point to mobi_unpack.py
			If not specified, searches in the current directory

After used once, mobi2mobi and mobi_unpack paths will be saved as default and are not necessary to include every time.
You can also drag and drop a number of mobi files onto the exe after the mobi2mobi and mobi_unpack paths have been saved.

After downloading the terms from Shelfari, they will be exported to a .aliases file in ./ext, named after the book's ASIN. The alias file allows you to define aliases for characters/topics manually to maximize the number of excerpts found within the book.
Initially I had it so that terms would automatically search by a character's first name as well (Catelyn Stark would also search for Catelyn) but issues arose for things like "General John Smith" so I have left it as a manual feature for now.
Aliases follow the format:
Character Name|Alias1,Alias2,Etc

Similarly, chapters are automatically detected as well if the table of contents is labelled properly, then exported to a .chapters file in ./ext. This allows any random chapters you may not want included, like copyright pages, acknowledgements, etc. Also allows you to setup parts in case the book is divided into parts that include multiple chapters.
Chapter format:
Name|start|end