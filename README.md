# Talk To Me Goose (TTMG)

A keyboard-driven script manager and terminal runner using Lua and Spectre.Console.

## Configuration (scripts.yaml)

The application is configured via `scripts.yaml` in the root directory.

*   `iMakeNoMistakes`: Boolean. If true, auto-executes a script if it is the only match for the current input.
*   `suppressUpdateChecks`: Boolean. If true, disables the automatic update check on startup.
*   `updateDirectory`: String. Directory where updates are downloaded.
*   `defaultShell`: String. The shell used for terminal commands (cmd, powershell, pwsh, bash, zsh, sh).
*   `versionUrl`: String. URL for the version JSON file.
*   `scripts`: List of manual script entries with name, path, and alias.
*   `repositories`: List of remote script repositories with shortName, url, and optional GitHub PAT token.
*   `commands`: List of global shortcodes and actions.

## Script Discovery

TTMG automatically discovers .lua files in the current directory and subdirectories.

*   Bundle Scripts: Folders containing an `init.lua` file are treated as a single script named after the folder.
*   Dot Folders: Discovery includes folders starting with a dot (e.g., `.tools/init.lua`).
*   Disambiguation: If multiple scripts share a name, the parent folder names are prepended using the `folder-script` format until unique.
*   Numbering: Every discovered script is assigned an index for quick selection.

## Interaction Modes

Switch between modes using the TAB key.

### Command Mode (goose>)
*   Type a script name or alias and press Enter to execute.
*   Type a script index (number) and press Enter to execute.
*   Type `` followed by a command to run it in the configured default shell.
*   Type `:qq` or `:wq` to exit.
*   Type `:update` to manually check for application updates.
*   Type `:install <repo-shortname> <script-name>` to download scripts from a repository.

### Menu Mode
*   Search/Filter: Type characters to filter the visible list.
*   Navigation: Use Up and Down arrow keys to highlight a script.
*   Execution: Press Enter to run the highlighted script.

## Lua API

Scripts have access to the `env` object and global helper functions.

*   `print(text)`: Prints text to the console. Supports Spectre.Console markup (e.g., `[red]text[/]`).
*   `prompt_input(title)`: Displays a text input prompt and returns the string.
*   `prompt_select(title, options_table)`: Displays a selection menu and returns the chosen string.
*   `run_process(command, args, detached)`: Executes a specific process with arguments.
*   `run_shell(command, detached)`: Executes a command using the `defaultShell` configured in yaml.