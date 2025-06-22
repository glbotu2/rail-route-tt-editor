# rail-route-tt-editor
WPF Timetable Editor for [Rail Route](https://store.steampowered.com/app/1124180/Rail_Route/)

## How to Use
- Make sure that your stations and map have already been set up. This is designed to make Editing larger timetables easier than hand-editing the trains.txt file. It doesn't know anything else about the map that isn't in that file.
- In RailRoute - when editing your game, go to Contracts -> Export to trains.txt
- Open RailRouteTimeTableEditor
- Click "Open trains.txt" and pick your trains.txt file (usually found at `C:\Users\[USERNAME]\AppData\LocalLow\bitrich\Rail Route\community levels`).
- To add a new train, fill in the base information "Headcode" (Train Identifier), "Penalty", "Train Composition" (made of L, C, P), "Max Speed" and "Train Type".
- Select a Starting station.
- Select its platform.
- Edit the times.
- Selecting another station and platform will add it to the train diagram.
- The platform and times can be edited/deleted.
- When you're happy with it, click "Add Train".
- To add another train with a new headcode, click "New Train". This will clear the currently editing train.
- To edit a train, click "Edit" on an entry from the list.
- You can sort by Headcode or Initial Arrival Time, and you can filter by Collision (see below) or headcode.
- When you're happy with your timetable, simply click "Save" or "Save As" - to be reimported, the file has to be called `trains.txt` and sit within the correct directory. As this tool is quite aggressive, it might be recommended to back up your existing trains.txt, just to be sure.
- Open Rail Route up again.
- Edit Route
- Go to Contracts -> Import trains.txt

## Error checking
- If you load a previously hand-edited trains.txt, it will check whether the entries are valid.
- It will also check for duplicate entries and sufficient timetable entries, before letting you save.
- A "Collision" checker runs in the background. This looks across your entire timetable and ensures that no two trains are at the same platform within a minute of each other. This will mark as "Collision" with a "View" button on any entry with a collision. The "View" will tell you which other 
train it collides with at what place and time. To resolve the collisions

## How to compile
- Open `RailRouteTimeTableEditor.sln` in Visual Studio Community Edition 2022. 
- Build/Rebuild. Or press the play button.
- Only tested on Windows - but it's .NET 8 so should compile for Linux systems.

## Coding Standards
- Coding standards are currently poor. This was built as a quick and dirty way to make new timetables in Rail Route. 