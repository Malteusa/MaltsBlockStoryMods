# Malt's Block Story Mods

A collection of simple Quality of Life (QoL) and gameplay tweak mods for Block Story, built using [BlockStoryModKit](https://github.com/1MR1C1/BlockStoryModKit).

*Massive thanks to 1MR1C1 for making BSMK!*

## Installation

> You **must** install [BlockStoryModKit](https://github.com/1MR1C1/BlockStoryModKit) first!

> In [Releases](https://github.com/Malteusa/MaltsBlockStoryMods/releases) there is **`All_Malts_Modpack.zip`** that contains "all" the mods bundled together so you can easily install them all at once using **BSMK's "Install mod/pack"** option

1. Download your desired `.dll` file.
2. Drag and drop the `.dll` file into your BepInEx plugins directory:  
   `\BepInEx\plugins\`
3. *(Or Simply)* Install directly via **BlockStoryModKit**.

> *Please check key mappings in-game or use BSMK's "Check hotkey conflicts" to find any possible conflicts with other mods*

> ***NOTE:** `Eldriar Tweaks`, `Onyx Tweaks` and `Mounted Mech Attack` will show a keybind conflict but it can be ignored, since they don't actually conflict during gameplay.*

## Modlist

* **Fishing Made Easy**: The first mod that led me into modding Block Story. Fishing kinda sucks, so this mod adds some tweaks to make it not suck.
  > Configurable using a Keybind menu *(Default: `[` Left Bracket)*
  * Automatically catch fish when they bite.
  * Disable the 50% chance for you to fail catching a fish.
  * Enable secondary fishing loot, even on a failed catch.
  * Configurable secondary loot multiplier, up to 100x.
---
* **Zoom Key**: A mod that adds a way to zoom the camera view.
  > Uses a Keybind *(Default: `Z`)*
  * Zoom the camera using a hotkey.
  * Zoom in or out using the mouse scroll wheel.
  * Screenshots taken while active will capture the zoomed-in camera view.
---
* **Eldriar Tweaks**: A highly configurable mod solely focused on making Eldriar actually worth your time.
  > All features can be toggled using the in-game mods list config.
  * Shoot Fireballs and Meteors on Right-Click when mounted.
  * Option to make it shoot when the key is held instead. **(Disabled by Default)**
  * Make Eldriar shoot fireballs and meteors more frequently.
  * Make Fireballs and Meteors home in on targets.
  * Make Fireballs and Meteors faster.
  * Protect friendly NPCs from Fireballs/Meteors *(~except the ones in his idle animation...~ Well, sometimes...)*.
  * Projectiles are automatically cleaned up once the enemy dies.
---
* **Onyx Tweaks**: A highly configurable mod solely focused on making the Phoenix Pet (Onyx) worthy of an endgame pet.
  > All features can be toggled using the in-game mods list config.
  * Shoot Fireballs on Right-Click when mounted.
  * Option to make it shoot when the key is held instead.
  * Increase the speed of the fireballs, can be disabled.
  * Make Onyx shoot fireballs more frequently.
  * Now shoots 8 fireballs instead of 5.
---
* **Mounted Mech Attack**: A highly configurable mod focuses on firing rockets on the Mech while mounted.
  > All features can be toggled using the in-game mods list config.
  * Fire rockets on Right-Click when mounted, as long as you have rockets in the Mech's inventory.
  * Option to make it fire when the key is held instead. **(Disabled by Default)**
  * Rockets are faster and home in better. Vanilla rocket speed can be enabled in the config.
  * Rockets fired by the Mech no longer destroy terrain. Terrain Destruction can be enabled in the config.
---
* **Mount Visibility**: Adds a Keybind to hide whatever mount, pet, or vehicle you are riding or have equipped.
  > Uses a Keybind *(Default: `H`)*
  * Hide any mount, vehicle, or pet so you can actually see your screen.
  * Works on equipable vehicles like the Jetpack and Diving suit.
  * Any hidden mount will automatically become visible again once dismounted.
---
* **Instant Anvil**: Fully configurable tweaks to the Block Story anvil, ranging from porting Minecraft's durability merging feature to Instant Anvils and Hammer Repairing.
  > All features can be toggled using the in-game mods list config.
  * Durability merging feature: combine 2 of the same tool/armor to transfer durability.
  * Instant Repairing using the anvil. **(Disabled by Default)**
  * Hammer Repairing.
---
* **Anvil Merge**: A **standalone** mod for implementing Minecraft's durability merging feature with anvils. Simply add 2 of the same armor/tool to transfer durability or merge them.
  > **Note:** Instant Anvil already includes this feature, so it is recommended to use one or the other.
---
* **Unlimited Pets**: A simple mod that removes the 5-pet summon limit.
---
* **Double Doors**:  A simple mod that makes both doors open/close at once in sync if one of them opens/closes.
---
* **Tool Stacking**: A mod that makes all tools, weapons and armor automatically "stack" and combine durability when picked up and manually in your inventory. Also makes Fuel finally stack in your inventory. Essentially the same way equipable armors can "stack" in vanilla.
  > Configurable using a Keybind menu *(Default: `,` Comma)*
  * Make all tools, weapons and armor "stack" on pickup.
  * Allows merging of tools, weapons and armor in the inventory by dragging them into the same item to merge durability.
---
* **Instant Soul Catch**: Made solely because suicidal Stormbringers were pissing me off.
  > Configurable using a Keybind menu *(Default: `I`)*
  * Make Soul Catching instant.
  * Remove the mana cost for Soul Catching.
---
* **Max Loot**: Makes you incomprehensibly lucky. Modifies the loot from Safeboxes, Mobs and Blocks.
  > Configurable using a Keybind menu *(Default: `\` Backslash)*
  * Force all naturally generated Safeboxes to have all 16 slots filled with loot.
  * Make all mobs drop all of their loot 100% of the time,
  * Make special loot ignore their mob level requirement.
  * Make mobs drop their loot without player/pet damage, just like the old days.
  * Make all blocks and mobs drop coins and diamonds on death/breaking.
---
* **No Save on Diamond Change**: A simple mod recommended for use with Max Loot if diamond drops on every mob kill and block breaking is enabled. Block Story forces an auto-save every time you gain or spend a diamond; this mod simply disables that forced save.
---
* **Knockback Tweaks**: Adds a Keybind menu that with a slider to multiply the knockback everything takes. Made just for fun. Knockback values aren't saved when the game closes by default, there is an option to keep it saved in the Keybind menu.
  > Configurable using a Keybind menu *(Default: `.` Period)*
---
* **Hide Diamonds**: Adds a Keybind that simply hides the Diamonds display at the top of the screen.
  > Uses a Keybind *(Default: `O`)*
---

## License

This project is licensed under a custom permissive modding license, see the full [LICENSE](https://github.com/Malteusa/MaltsBlockStoryMods/blob/main/LICENSE) file for details.

### What you CAN do:
* **Download & Play:** Free to use for personal gameplay.
* **Inspect & Learn:** You are free to decompile, disassemble, and reverse engineer the code.
* **Modify & Fork:** You are welcome to adapt, modify, and create derivative works, provided public releases remain non-commercial and include full source code.

###  What you CANNOT do:
* **Sell or Monetize:** You may not sell, rent, or commercially exploit these mods or any derivative works.
* **Rebrand or Claim Ownership:** You may not remove copyright notices or present the original work as your own.
* **Use Outside Block Story:** All mods and derived code must be used strictly for modding Block Story.
---
### Disclaimer
This is an independent, unofficial set of mods and is not affiliated with, authorized, or endorsed by **Big Cube Interactive LLC** or the makers of **Block Story**. Block Story is a registered trademark of Big Cube Interactive LLC.
