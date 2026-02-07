print("Welcome to [cyan]Talk To Me Goose[/] sample script!")

local name = prompt_input("Enter your pilot callsign:")
print("Greetings, [bold yellow]" .. name .. "[/]. Ready for departure.")

local task = prompt_select("Select mission objective:", {
    "Scan for enemies",
    "Return to base",
    "Execute custom command",
    "System Status"
})

print("Objective updated to: [bold green]" .. task .. "[/]")

if task == "Execute custom command" then
    local cmd = prompt_input("Enter shell command to run:")
    run_process("cmd.exe", "/c " .. cmd, false)
elseif task == "Scan for enemies" then
    print("[red]Scanning...[/]")
    run_process("cmd.exe", "/c timeout /t 2", false)
    print("[bold red]No enemies found in sector.[/]")
elseif task == "System Status" then
    run_process("cmd.exe", "/c systeminfo", false)
end

print("[dim]Mission complete.[/]")