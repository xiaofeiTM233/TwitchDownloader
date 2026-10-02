# User Instruction Memory

This file records user instructions, preferences, and teachings for reference in future interactions.

## Format

### User Instruction Entry
User instruction entries should follow this format:

[User Instruction Summary]
- Date: [YYYY-MM-DD]
- Context: [Mentioned scenario or time]
- Instructions:
  - [Content of user teaching or instruction, described line by line]

### Project Knowledge Entry
Entries discovered by the Agent during task execution should follow this format:

[Project Knowledge Summary]
- Date: [YYYY-MM-DD]
- Context: Discovered by Agent while performing [specific task description]
- Category: [Operations & Deployment|Build Methods|Testing Methods|Troubleshooting & Debugging|Workflow & Collaboration|Environment Configuration]
- Instructions:
  - [Specific knowledge points, described line by line]

## Deduplication Strategy
- Before adding a new entry, check for similar or identical instructions.
- If a duplicate is found, skip the new entry or merge it with the existing one.
- When merging, update the context or date information.
- This helps avoid redundant entries and keeps the memory file tidy.

## Entries

[Project Knowledge Summary]
- Date: 2026-10-01
- Context: Discovered by Agent while porting TwitchDownloaderWPF to TwitchDownloaderAvalonia (Avalonia 11.3.22, net10.0) and iterating on Release builds
- Category: Build Methods
- Instructions:
  - Build with `/root/.dotnet/dotnet build -c Release` inside `TwitchDownloaderAvalonia/` (SDK 10.0.401; dotnet not on PATH by default)
  - Long builds must run through `background_terminal_create` (CPU limit 200), read output from the terminal log file
  - `tail`-piped build commands report exit code of `tail`, always grep the log for `Build succeeded|Build FAILED`
  - Avalonia 11.3 breaking changes hit during migration (apply to any future XAML work here):
    - `ColorPicker` lives in the separate NuGet package `Avalonia.Controls.ColorPicker`; when the package is missing the XAML compiler emits no error, code-behind just fails CS0103 on the x:Name fields
    - `DataGrid` needs the `Avalonia.Controls.DataGrid` package plus `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml" />` in App.axaml
    - `NumericUpDown.Value` is `decimal?` (numeric assignments need `(decimal)` casts)
    - `AvaloniaLocator.Current` is gone; use `Application.Current.PlatformSettings` for `IPlatformSettings`
    - `Dispatcher.UIThread.InvokeAsync(Func<Task<T>>)` already returns unwrapped `Task<T>` (no `.GetTask()`/`.Unwrap()`)
    - `StorageProvider.TryGetFolderFromPathAsync` requires `using Avalonia.Platform.Storage;` and takes a `Uri` overload path
    - `DynamicResourceExtension` cannot be assigned to properties from C#; use `element.Bind(property, Application.Current.Resources.GetResourceObservable(key, x => (IBrush)x))` (see Services/DynamicResourceHelper.cs)
    - `IPlatformSettings.ColorValues` property became `GetColorValues()` method
    - In XAML, `&#123;` entity escapes decode before markup-extension parsing, so `Text="&#123;pix_fmt&#125;"` still fails AVLN2000; use the `{}` escape: `Text="{}{pix_fmt}"`
    - Avalonia `TextBox` has no `VerticalScrollBarVisibility` attached property (that is ScrollViewer's); Avalonia TextBox scrolls internally
    - AVLN3001 "no public constructor" warnings on windows with required ctor parameters are benign (only affects avares:// runtime loading)

[Project Knowledge Summary]
- Date: 2026-10-02
- Context: Discovered by Agent while validating the Avalonia publish command used by the new GitHub Actions workflow (.github/workflows/build-avalonia.yml)
- Category: Build Methods
- Instructions:
  - Cross-platform publish command (validated locally on linux-x64, ~118MB self-contained single-file ELF):
    `dotnet publish TwitchDownloaderAvalonia -c Release -r <rid> --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=Embedded -o <out-dir>`
  - Repo publish profiles live in `TwitchDownloaderCLI/Properties/PublishProfiles/` and `TwitchDownloaderWPF/Properties/PublishProfiles/` (SelfContained + PublishSingleFile); TwitchDownloaderAvalonia has none, flags are passed via CLI
  - GitHub workflow `build-avalonia.yml` triggers on workflow_dispatch, push to ai/1 (paths: TwitchDownloaderAvalonia/** or the workflow file), and PRs; per-RID zip artifacts named `TwitchDownloaderGUI-Avalonia-<rid>`
  - macOS builds skip ffmpeg bundling (no reliable static arm64 download); Windows uses gyan.dev essentials zip, Linux uses johnvansickle static tar.xz
  - `http.postBuffer` was raised to 500MB in this clone because pushes of the large Avalonia commit failed with HTTP 408
  - `dotnet publish/run` of the apphost binary needs `DOTNET_ROOT=/root/.dotnet` (or PATH) or it fails with ".NET location: Not found"
  - UI text rendered blank everywhere: `LocExtension` bound the indexer via `Path="Item['{Key}']"`, which Avalonia 11's binding path parser fails on silently. Fixed by binding `Path="Culture"` with a `LocKeyConverter` (ResourceManager lookup keyed by ConverterParameter); culture switch still refreshes everything via PropertyChanged. Never use quoted string-indexer binding paths in Avalonia.
  - Visual UI verification works headlessly: `Xvfb :99` + `DISPLAY=:99 DOTNET_ROOT=/root/.dotnet ./TwitchDownloaderAvalonia` then `DISPLAY=:99 import -window root shot.png` (packages: xvfb, imagemagick, fonts-dejavu-core)
