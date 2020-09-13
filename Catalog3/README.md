
# Catalog3: Release Notes

[image](https://imgur.com/a/b6TP7Y3)

**Latest Download Link:**

[https://mega.nz/folder/m2p0FYrL#ZeYuo66T39qbEO5l1q_rZQ](https://mega.nz/folder/m2p0FYrL#ZeYuo66T39qbEO5l1q_rZQ)

# Revisions:

* V3.0: [https://mega.nz/folder/m2p0FYrL#ZeYuo66T39qbEO5l1q_rZQ](https://mega.nz/folder/m2p0FYrL#ZeYuo66T39qbEO5l1q_rZQ)
* V2.1: [https://mega.nz/file/ry5hnJJR#xiHkQMqswBwBpRoZ6uyi972xyuBIisGUWPvbuKgkLjc](https://mega.nz/file/ry5hnJJR#xiHkQMqswBwBpRoZ6uyi972xyuBIisGUWPvbuKgkLjc)
* V2.0: [https://mega.nz/file/PioBGTCQ#HVxKQDixcHh8yGjDP5ck232n\_5SvUmETL\_L34dKaOMU](https://mega.nz/file/PioBGTCQ#HVxKQDixcHh8yGjDP5ck232n_5SvUmETL_L34dKaOMU)

# Summary:

* Create your own catalogs (Capture mode).
* Switch between outfits, hairstyles and morphs easily.
* Create catalogs to save and share in your own scenes (I grant thee permission to do it).
* Generate new faces, or other morphs in FaceGen mode.

* Can add to Session or to Person

# Description

## Improvements in 3.0 include:
* A cool UI
* Mannequin helpers
    * Select/alter control points in scene
    * Add triggers and animations to control points
* Preset catalog quick menu
* Center Pivot feature
* Capture object mode
* Capture pose
* Capture active morphs
* Session and Player mode
* Global action "Apply entry at next/random/atIndex" (for triggers)

## Revision Notes 2.1

## Modes

* ***View Mode***: This removes any extra buttons and just shows the catalog. Nice for sharing in scenes when you're done creating the perfect catalog.
* ***Person mode***: Gives you the "Capture" button and capture toggles,
   * Clothing items
   * Hair styles
   * Active Morphs (morphs adjusted manually)
   * Pose
* ***Face-Gen Mode***: Generates a number of random faces based on the selected morph options, variance, and count in the plugin settings.
* ***Scenes-Directory Mode***: Shows all scene images in the current folder, and allows you to switch to the scenes.
* ***Object Mode***: Available when you add Catalog Plugin to an object, the object becomes the default atom if no atom is selected. Captures the state of the Object. Gets auto-loaded with the scene.
* ***Session Mode***: Captures the state of the Selected Object. Will persist when changing scenes.

## Session/Atom mode
* ***Atom Mode***: When you add Catalog Plugin to an atom, the atom becomes the default if no other atom is selected. Catalog gets auto-loaded with the scene.
* ***Session Mode***: When you add Catalog Plugin to the session. Will persist when changing scenes. You must select atoms in the scene to apply atom-specific mutations.

## Setting Description
**General Settings**
* Show Catalog: Shows the catalog UI if it was hidden.(from pressing the "X" button)

**Play Settings**

* Apply entry and move next: Applies the currently selected catalog entry, and then moves to the next entry.
* Apply random entry: Applies a random entry from the catalog.
* Morph Transition time: For Morphs and Poses, the amount of time in seconds to change to the selected entry.
* Play: Continue applying entries until Stop is pressed. Entry selection is based on the buttons above (next or random)  
* Interval: Amount of time between each frame when playing the catalog.
* Play Catalog once: Play the amount of entries in the catalog from the current position.

**Catalog Settings**

* Catalog Mode: (described above in *Modes)*
* Reset Catalog: Removes all catalog entries.
* Catalog Columns: Select how many horizontal entries you want in each row.
* Transparency: Set the transpartency of the catalog images. (Usefull for when you want to overlay an image over you model)
* Active Camera: (buggy) Select the camera to screenshot with.
* Visible: Hides the in-game UI if unchecked
* Always Face Me: Makes the in-game UI 2-D so that it always appears flat despite the scene angle.
* Anchor on HUD: Attaches the UI to the HUD.
* Anchor on Atom: Attaches the UI to the selected object in the scene.
* Overlay Mutations: Keeps the previous mutation.

**Capture settings** (for Capture Mode)

* Capture: Captures the state of the current scene (same as the in-game Capture button)
* Capture Hair: Include the current hair style when capturing
* Capture Clothes: Include the current clothing items when capturing the scene
* Capture Facegen morphs: Include any generated mutations (from Mutations mode)
* Capture Pose: Capture the current pose
* Remove Unused Items: The items for each catalog entry appear in the right column of the settings. You can deselect them but they won't go away. This button will remove unselected items from the entry permanently. Nice for cleaning out items captured by accident.
* Select custom image: Allows you to select your own image from file for the catalog entry.

**Face-Gen Settings** (for Face-Gen Mode)

* Generate Random Mutations: Generates a sequence of mutations and lists them in the catalog (Same as "GENERATE RANDOM MUTATIONS" UI button)
* Generate entries: The number of random mutations to generate when using the "Generate Random Mutations" function.
* The amount of time to wait from applying the mutation to screen-capturing the scene. There are some morphs which, when changed, will upset the scene, and it may take some time for the scene to settle again. Adjusting this value specifies how long to wait after applying the mutation. If you don't wait long enough then you'll capture the scene in an active state. Whether you need to adjust this setting depends really on what morphs you're updating.
* Mutation Variance: Adjusts how much morphs can be possibly changed when generating random mutations. The values are still random though so you're not guaranteed to get more extreme changes, it just increases the likelihood.
* Retry mutation: Remove the previous mutation, and generate 1 new mutation.
* Undo mutation: Remove the previous mutation.
* Keep Mutation: Sets the current mutation so that the next mutation occurs on top of the current mutation.
* Retry Hair: Applies a random hair style.
* Add Clothing Item: Adds a random Clothing Item
* Retry Clothing Item: Removes the previously added clothing item, and add a new clothing item.
* Undo Clothing: Removes the previously added clothing item.
* Add Whole Appearance: Adds a morph, clothing item and hair item.

**Morph Selection**

...the morphs that you want the random morph generator to use.

&#x200B;

# Credits:

Special thanks to "**chokaphi**" for the "**Active Morphs plugin"** which provided the initial UIHelper logic.[https://www.reddit.com/r/VAMscenes/comments/c3rwuc/activemorphs\_a\_plugin\_to\_show\_only\_active\_morphs/](https://www.reddit.com/r/VAMscenes/comments/c3rwuc/activemorphs_a_plugin_to_show_only_active_morphs/)

# Patreon Page

[Patreon Page](https://patreon.com/user?0=u&1=%3D&2=3&3=0&4=0&5=1&6=7&7=1&8=2&9=8&utm_medium=social&utm_source=twitter&utm_campaign=creatorshare)

# Repository on Git
[https://github.com/KeepCalmAndDoABackflip/VAM.Plugin.Catalog](https://github.com/KeepCalmAndDoABackflip/VAM.Plugin.Catalog)
