# ShyGrowers
Comfy Jam 2026 -- Wizen, Silas, and Brandon

## Project layout
- `Godot/` contains the Godot project. Open `Godot/project.godot` in Godot.
- `Godot/ShyGrowers.web/` contains the 2dog web host project.
- `Publishing/WebBuild/` is where the build files from Godot/ShyGrowers.web are published.

From `Godot/`, publish and preview the web build with:

```sh
dotnet publish ShyGrowers.web
dnx dotnet-serve -- -d ../Publishing/WebBuild
```

Run `dnx 2dog add --web` once to create the web host project. Upload `Publishing/WebBuild/` to deploy.
Use `dotnet-serve` for locally running the game through a browser (localhost).
Visit [this link](https://2dog.dev/hosts/web) if anything is unclear
