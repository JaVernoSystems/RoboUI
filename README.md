# RoboUI

**Robocopy power without the command-line headache**

![Version](https://img.shields.io/badge/version-1.0.0-blue)

---

## ***!!!Security Note!!!***

RoboUI is a graphical front-end for Windows **Robocopy**, a built-in system utility.  
It ***does not*** connect to the internet, collect data, or transmit information.

During installation, Windows may display standard capability prompts related to packaged desktop applications. These are part of the MSIX packaging model and do not indicate network activity by RoboUI itself.

---

## Overview

RoboUI is a Windows utility that provides a user-friendly interface for **Robocopy**, the built-in high-performance file copy tool included with Windows.

This project was created to solve a real-world need: making fast, reliable file transfers easier to manage without memorizing command-line switches. It also serves as a portfolio demonstration of designing a practical UI layer around an existing system tool while preserving performance and stability.

RoboUI does **not** replace Robocopy — it launches and monitors it.

---

## Why RoboUI Exists

File Explorer copies are fine until you need:

- Reliability  
- Speed  
- Logs  
- Repeatable jobs  

That’s where Robocopy shines — but the command line isn’t for everyone.  
RoboUI bridges that gap without slowing anything down.

---

## Key Features

- Fast file transfers using Robocopy’s multi-threading  
- Saveable job presets for repeat tasks  
- Live output and summary view  
- Optional log file generation  
- Safety warning for mirror operations  
- Lightweight and self-contained  

---

## File Explorer vs RoboUI

| Capability | File Explorer | RoboUI |
|------------|---------------|--------|
| Multi-threaded copy | No | Yes |
| Retry control | No | Yes |
| Resume on interruption | Limited | Yes |
| Job presets | No | Yes |
| Logging | No | Yes |

---

## Mirror Mode Warning

Mirror mode (`/MIR`) can remove files from the destination that do not exist in the source.

Always double-check your paths before running.

---

## Requirements

- Windows 10 or 11  
- Robocopy (included with Windows)  
- .NET Desktop Runtime  

---

## Basic Usage

1. Choose source and destination folders  
2. Select desired options  
3. Click **Run**  
4. Optionally save the configuration as a job  

---

## Logging

Logs use Robocopy’s native logging system, ensuring accurate reporting without performance impact.

---

## What RoboUI is NOT

- Not a backup solution  
- Not cloud sync software  
- Not a background service  
- Not a replacement for Robocopy  

It is a convenience layer for a trusted Windows tool.

---

## About This Project

RoboUI began as a personal productivity tool and evolved into a portfolio project to demonstrate:

- Safe integration with system tools  
- Performance-aware UI design  
- Practical utility software development  

It is not flashy.  
It is not bloated.  
It simply makes Robocopy easier to use.

---

## Install RoboUI

1. Download all files
2. Right-click **RoboUI.Package_1.0.0.0_x64.msix**
   - Install Certificate
   - Local Machine
   - Place in **Trusted People**
3. Double-click **RoboUI.Package_1.0.0.0_x64.msix**
4. Click Install
