# VideoControllerUIT
 Unity Video Player wrapper plugin for Virt-A-Mate

## Description 

VideoControllerUIT (Updates and fixes for UI Triggers) 

Allows you to play videos directly from local storage on your PC using Unity Video Player rather than needing to use WebPanels. Configurable to play multiple videos at once in a variety of different layouts, and accepts triggers to enable the construction of complex player UI. 

This package contains updates and bug fixes to VamSander's original VideoController script resource, which appears to have been abandoned. However, if VamSander returns and wants to collapse this back into his original resource, I will be very happy to close out my version! (And should I disappear in the future, please feel free to repackage and post any additional fixes or tweaks that others may make to the script.)

## Credits

VamSander, who wrote the original version of this script:
https://hub.virtamate.com/resources/videocontroller.9797/

redeyes, who contributed fixes and tweaks to the original resource posting by VamSander
Eye9, for aspect ratio work, PlayNext, PlayPause, Stop, and Skip triggers


## Instructions

Attach this script as a plugin to any atom (I generally just use a simple invisible cube), and configure it to point to a path where videos exist on your computer. It can play a wide variety of video formats, but only has direct access to browse locations within your VAM directory. A trigger exists to feed in files from other locations, or you can use symlinks/hardlinks/folder junctions to indirectly point it outside your VAM directory. (Note: Windows shortcuts will NOT work, only symlinks and junctions!)
