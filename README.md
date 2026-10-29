# ProjectAssetman

Mod Manager for Project Wingman that seeks to combine modified datatables. Does so somewhat decently. Should be refined more later.

Proudly made by idiot hands without Agentic Interference (that is, AI slop).


lizard's laziest tutorial:

so what PAM does in order of first launch to pak'd mod:
- extracts the vanilla datatables from PW (this is the first launch)
- turns the properties of each datatable into a JSON that she can use to apply patches via jsons provided by mods (this happens during the R entry)
- extract all of the files from each mod to its own subfolder in mod extracts (e for extract)
- copies the datatables into a single separate folder for easier referencing in mod datatables (ending of E)
- during this, PAM also creates a json that describes all of the unique assets a mod references (this is, the contents of the mod), which datatables it refers to specifically, and the author's name
- performs the same json creation it did to vanilla files to each mod, this time not saving the json if its entries are an exact match to vanilla's (if even a single property matches, it'll save the json) (b for build databases)
- scans asks the user which mods they want her to combine based off of what mod jsons she can see (these were made during E)
- copies all of the files from those mods to a new directory for paking later
- asks about F59 and prez
- loads vanilla datatables, applies patches and additions from the mods, and saves them to a created mod directory
- after all datatables have been patched, she moves the datatables she saved into the right spot by matching them with the pre-existing datatables that she copied before
- then paks the file for you
