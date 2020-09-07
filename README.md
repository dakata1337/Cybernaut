# Cybernaut

> Cybernaut is a Discord bot written in C# Core.


## Table of Content

* [Installation](#installation)
* [Commands](#Commands)
* [Support](#support)
* [Report bugs](#Report-bugs)
* [Acknowledgments](#Acknowledgments)

## Built With

* [DotNet Core (Version - 3.1)](https://dotnet.microsoft.com/download/dotnet-core/2.2) - DotNet Core framework
* [Discord.Net (Version - 2.3.0-dev-20200904.2)](https://github.com/RogueException/Discord.Net) - Discord Library
* [Victoria (Version - 5.1.2)](https://github.com/Yucked/Victoria) - LavaLink Library
* [LavaLink (Version - 3.3.1.1)](https://github.com/Frederikam/Lavalink) - LavaLink

## Installation

1. Download the repo on your PC.
2. Once it's on your machine you will want to run the command: dotnet restore (in the terminal aka cmd).
NOTE: Make sure you do this in the same directory as your .sln file.
3. Download LavaLink from [here](https://github.com/Frederikam/Lavalink/releases/) and the example config from [here](https://gitlab.giesela.ch/shikhirarora/Lavalink/raw/081509b7324a2c34dcb903dd57a5f3b2e27529e2/LavalinkServer/application.yml.example?inline=false) and save it as application.yml.
4. Download Java JDK 13 from [here](https://www.oracle.com/java/technologies/javase-jdk13-downloads.html).
5. CD into LavaLinks directory by typing:
```cd C:\Users\User\Desktop\Cybernaut```
NOTE: This directory is just an example.
6. Start LavaLink by typing:
```java -jar LavaLink.jar```
7. Open Visual Studio and build the bot.
8. To start the bot you can use the built in debugger in VS or run it from the executable.

NOTE: The first time you start the bot will create a config file which you need to edit. In order for the bot to work.
---

## Commands
* Status - Information about the bot ```Usage: !status```
* Join - The bot joins the channel you are in ```Usage: !join```
* Leave - The bot leaves the chat he was in ```Usage: !leave```
* Play - The bot joins if not already and starts playing the selected song ```Usage: !play (youtube link/song name)```
* Stop - The bot stops playing music and clears the playlist ```Usage: !stop ```
* List - Shows the songs in the queue ```Usage: !list```
* Skip - Skips to the next song ```Usage: !skip```
* Loop - Loops the song that is currently playing / shows if looping is enabled ```Usage: !loop / !loop status```
* Volume - Set the bot music volume ```Usage: !volume (from 1% to 150%)```
* Pause - Pause the music ```Usage: !pause```
* Resume - Resumes the music ```Usage: !resume ```
* Auth:<br/>
-enable - Enables on join user authentication ```Usage: !auth enable ```<br/>
-disable - Disables on join user authentication ```Usage: !auth disable ```<br/>
-role - The role that is given when authenticated ```Usage: !auth role @role ```<br/>

## Support
If you have any questions feel free to join my discord and ask your question in the chat room called "Questions" in the category "Programing"

[Discord Server](https://discord.gg/DmCrpuf)

## Report-bugs
If you find any errors or things not supposed to happen please go to the tab "Issues" and report the bug. Please answer this questions:
* When and how did the error occurred.
* What error is it sending in Discord.

## Acknowledgments

* [Frederikam](https://github.com/Frederikam) - For making the library [LavaLink](https://github.com/Frederikam/Lavalink)
* [Yucked](https://github.com/Yucked) - For making the library [Victoria](https://github.com/Yucked/Victoria)
* [metaldream64](https://github.com/metaldream64) - For the 'Professional' translation