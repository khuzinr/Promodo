# Promodo

Promodo Timer (vibe coding)

## Requirements
- [.NET SDK 8.0](https://learn.microsoft.com/dotnet/core/install/linux) or newer

## Install .NET SDK on Debian/Ubuntu
1. Install prerequisites:
   ```bash
   sudo apt-get update
   sudo apt-get install -y wget apt-transport-https gpg
   ```
2. Add the Microsoft package feed:
   ```bash
   wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   rm packages-microsoft-prod.deb
   ```
3. Install the SDK:
   ```bash
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-8.0
   ```

If you need a different distro or version, follow the official instructions: <https://learn.microsoft.com/dotnet/core/install>.

## Build
```bash
dotnet build
```
