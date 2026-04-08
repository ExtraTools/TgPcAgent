# TgPcAgent Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Windows tray app that polls a Telegram bot, binds itself to a single owner chat, and exposes a fixed set of PC management commands.

**Architecture:** Keep the Telegram command flow, pairing logic, confirmation workflow, and command parsing in a testable core library. Put Windows-specific integrations such as tray UI, screenshots, app launching, power actions, hardware monitoring, and config persistence in a WinForms host.

**Tech Stack:** .NET 8, WinForms, xUnit, Telegram.Bot, LibreHardwareMonitorLib

---

### Task 1: Solution Skeleton

**Files:**
- Create: `TgPcAgent.sln`
- Create: `TgPcAgent.Core/`
- Create: `TgPcAgent.Core.Tests/`
- Create: `TgPcAgent.App/`

**Step 1: Write the failing test**

Add a test project reference that currently cannot resolve the production types because they do not exist yet.

**Step 2: Run test to verify it fails**

Run: `dotnet test`
Expected: FAIL because command and security services are missing.

**Step 3: Write minimal implementation**

Create the solution and projects.

**Step 4: Run test to verify it passes**

Run: `dotnet build`
Expected: PASS for project scaffolding.

### Task 2: Pairing And Confirmation Core

**Files:**
- Create: `TgPcAgent.Core/Security/PairingService.cs`
- Create: `TgPcAgent.Core/Security/ConfirmationStore.cs`
- Create: `TgPcAgent.Core/Commands/BotCommandParser.cs`
- Test: `TgPcAgent.Core.Tests/Security/PairingServiceTests.cs`
- Test: `TgPcAgent.Core.Tests/Security/ConfirmationStoreTests.cs`
- Test: `TgPcAgent.Core.Tests/Commands/BotCommandParserTests.cs`

**Step 1: Write the failing test**

Verify that pairing codes are one-time use, unauthorized chats are rejected, and dangerous actions need two sequential confirmations.

**Step 2: Run test to verify it fails**

Run: `dotnet test TgPcAgent.Core.Tests`
Expected: FAIL because services are not implemented.

**Step 3: Write minimal implementation**

Implement the parser and security services with the smallest surface needed by the tests.

**Step 4: Run test to verify it passes**

Run: `dotnet test TgPcAgent.Core.Tests`
Expected: PASS.

### Task 3: Telegram Command Host

**Files:**
- Create: `TgPcAgent.App/Services/TelegramPollingService.cs`
- Create: `TgPcAgent.App/Services/CommandExecutionService.cs`
- Create: `TgPcAgent.App/Services/ConfigurationService.cs`
- Create: `TgPcAgent.App/Models/AppConfig.cs`

**Step 1: Write the failing test**

Cover command routing inputs that should map to known command types and callback payloads.

**Step 2: Run test to verify it fails**

Run: `dotnet test TgPcAgent.Core.Tests`
Expected: FAIL on new parser cases.

**Step 3: Write minimal implementation**

Add the polling loop, callback handling, and owner-only gating.

**Step 4: Run test to verify it passes**

Run: `dotnet test TgPcAgent.Core.Tests`
Expected: PASS.

### Task 4: Windows Tools

**Files:**
- Create: `TgPcAgent.App/Services/SystemStatusService.cs`
- Create: `TgPcAgent.App/Services/ProcessService.cs`
- Create: `TgPcAgent.App/Services/ScreenshotService.cs`
- Create: `TgPcAgent.App/Services/AppCatalogService.cs`
- Create: `TgPcAgent.App/Services/PowerService.cs`

**Step 1: Write the failing test**

Add or extend parser tests for status, processes, screenshot, scan apps, open app, and power actions.

**Step 2: Run test to verify it fails**

Run: `dotnet test TgPcAgent.Core.Tests`
Expected: FAIL until the new command cases exist.

**Step 3: Write minimal implementation**

Wire status, process listing, screenshot capture, scanned app resolution, and power actions with double confirmation.

**Step 4: Run test to verify it passes**

Run: `dotnet test TgPcAgent.Core.Tests`
Expected: PASS.

### Task 5: Tray UX And Packaging

**Files:**
- Create: `TgPcAgent.App/Tray/AgentApplicationContext.cs`
- Create: `TgPcAgent.App/Forms/SettingsForm.cs`
- Modify: `TgPcAgent.App/Program.cs`

**Step 1: Write the failing test**

No automated UI test is required; verify build-time wiring instead.

**Step 2: Run test to verify it fails**

Run: `dotnet build`
Expected: FAIL until host wiring is complete.

**Step 3: Write minimal implementation**

Add the tray icon, menu entries, pairing display, settings editing, log opening, and startup flow.

**Step 4: Run test to verify it passes**

Run: `dotnet test` and `dotnet build`
Expected: PASS.
