# VideoControllerUIT
 Unity Video Player wrapper plugin for Virt-A-Mate

## Description 

VideoControllerUIT (Updates and fixes for UI Triggers to VamSander's VideoController) 

Allows you to play videos of most video formats directly from local storage on your PC using Unity Video Player rather than the traditional solution of using WebPanels (which are a bit more cumbersome and have limited format support). Configurable to play multiple videos at once in a variety of different layouts, and accepts triggers to enable the construction of complex player UI. 

This package contains updates and bug fixes to VamSander's original VideoController script resource, which appears to have been abandoned. However, if VamSander returns and wants to collapse this back into his original resource, I will be very happy to close out my version! (And should I disappear in the future, please feel free to repackage and post any additional fixes or tweaks that others may make to the script.)

Changes from VamSander's original script:
Improved aspect ratio handling (Eye9)
New setting to control whether audio plays from all screens or just the first one
New and/or fixed triggers:
PlayNext (Eye9)
PlayNextFirstScreen
PlayPause (Eye9)
PlayPauseFirstScreen
Stop (Eye9) 
StopFirstScreen
Skip (Eye9)
Refresh/Clear
Volume from all screens


## Credits

VamSander, who wrote the original version of this script, found here:
https://hub.virtamate.com/resources/videocontroller.9797/

redeyes, who contributed fixes and tweaks to the original resource posting by VamSander
Eye9, for aspect ratio work, PlayNext, PlayPause, Stop, and Skip triggers


## Instructions

Attach this script as a plugin to any atom (I generally just use a simple invisible cube), and configure it to point to a path where videos exist on your computer. 

It can play a wide variety of video formats, but only has direct access to browse locations within your VAM directory. A trigger exists to feed in files from other locations, or you can use symlinks/hardlinks/folder junctions to indirectly point it outside your VAM directory. (Note: Windows shortcuts will NOT work, only symlinks and junctions!)

I suggest creating a directory called VAM/VideoPlayer in which to build your library of videos -- the included sample scene is configured to use that directory (as will be any other scenes that I publish in the future). Plus, it's clean, semantically clear, and matches the existing naming scheme for directories in VAM's base folder.


## Settings

Layout allows you to choose from a number of screen arrangement styles. (Note: Some are a bit experimental, and not all will play nicely with mixed aspect ratios.)
.
Number of Screens X and Y configures the number of screens in layouts that support configuration
.
Distance controls how far the screen will be projected from its base atom.

Curvature creates  a curved monitor effect.

Aspect Ratio attempts to help cue the players on how to manage aspect ratios. Eye9 has done some good work to improve things here, but sometimes they can still be a bit funky.

Screen Size scales the display size of all screens.

Loop Mode loops videos for each player rather than playing the next in the queue.

Animate In adds a transition animation when changing videos.

Rotate rotates the screen configuration around the attached atom.

Audio Volume controls the volume of audio from screens.

Audio on All Screens toggles whether audio will play from all screens or just the first/main screen in a layout.

Alphabetical Order changes queue order to alphabetical order rather than the default randomized playlist..



## Triggers

Most settings can be altered/monitored via trigger -- particularly useful for the Volume setting!

Play Once allows you to directly request any file path or URL to play on a screen, regardless of whether it's located within your VAM folder. Must be a path to an actual video -- ie, YouTube links won't work. Specify target screens and audio options like so:
E:/abcdef/4.mp4, screen:3, audio:false
https://somewebsite.com/vids/5.mpg, screen:0

Add Files works like Play Once, except it takes a list.

Video Path changes the currently loaded video path -- however, this does NOT clear the current playback queue, but rather changes values that will be loaded into the playback queue as playback continues.

Refresh/Clear clears the playback queue and resets it from the currently loaded Video Path value.

PlayNext directs all screens to play the next video in their queue.

PlayPause toggles playback on all screens.

Stop ends playback on all screens.

"FirstScreen" variants for PlayNext, PlayPause, and Stop, which affect only the first screen in a layout.




**Useful tip:**

If you want to create buttons to change "channels" for your VideoController instances, use the following sequence of triggers:

VideoPath
Refresh/Clear
PlayNext
