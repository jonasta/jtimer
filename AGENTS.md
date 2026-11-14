# Agent Rules for This Project

These rules apply to any AI agent or contributor making changes in this repository.

## Core rules

1. **Follow industry standards**
   - Write code that follows widely accepted .NET and Blazor conventions and best practices.
   - Prefer clear, maintainable solutions over clever or obscure ones.
   - Use framework/platform features idiomatically (ASP.NET Core/Blazor, C#, Razor components).

2. **Code must be human-readable**
   - Use descriptive, intention-revealing names for files, types, members, variables, and parameters.
   - Keep methods/components focused and reasonably small; extract logic when it improves clarity.
   - Add brief comments only when intent is not obvious from the code (avoid redundant comments).
   - Structure markup and code consistently so others can scan and understand it quickly.

3. **Document applied standards in README**
   - Any coding/formatting/architecture standards applied to this project must be described in `README.md` (see the "Project standards" section).
   - When introducing a new convention or tool (e.g. formatter, analyzer, folder structure rule), update `README.md` accordingly.

## Project conventions (Blazor/.NET)

- **Language & framework:** C# and Blazor WebAssembly on .NET (target framework defined in `jtimer.csproj`).
- **Naming:** Follow .NET naming conventions (PascalCase for types/methods/properties/components, camelCase for locals/parameters, meaningful names).
- **Components:** Keep `.razor` components focused; place reusable UI in appropriate folders and avoid unnecessary complexity.
- **Formatting:** Keep indentation and style consistent with existing project files; prefer readable layout in Razor markup and C#.
- **Simplicity:** Favor the simplest implementation that meets requirements; avoid unused code and unnecessary dependencies.
- **Changes:** Make minimal, relevant changes; preserve existing behavior unless the task requires a change.

## Before finishing work

- Ensure the project builds (e.g. `dotnet build`) when code changes are made.
- Update `README.md` if standards, structure, or setup/run instructions change.