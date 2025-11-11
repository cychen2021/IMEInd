# IMEInd Utility Tool

A .NET 10 utility tool for Windows.

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run
```

Or with arguments:

```bash
dotnet run -- --help
```

## Publishing

To create a self-contained executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The output will be in `bin/Release/net10.0/win-x64/publish/`
