# BbQ.Outcome Source Generators

A collection of C# source generators that enhance the [BbQ.Outcome](https://github.com/JeanMarcMbouma/Outcome) library by automatically generating utility methods and extension methods for the `Outcome<T>` type.

## Overview

This package contains source generators that automatically generate boilerplate code for working with the `Outcome<T>` result type. Source generators run at compile-time to produce additional code, reducing manual code writing and improving type safety.

## Features

- **Automatic code generation** at compile-time
- **Zero runtime overhead** - all generation happens during build
- **Type-safe** utilities for common Outcome operations
- **Seamless integration** with BbQ.Outcome

## Installation

### As Part of BbQ.Outcome

The source generators are automatically included when you install the main [BbQ.Outcome](https://www.nuget.org/packages/BbQ.Outcome) NuGet package:

```bash
dotnet add package BbQ.Outcome
```

### Standalone Package

If you want to use the source generators independently:

```bash
dotnet add package BbQ.Outcome.SourceGenerators
```

## Usage

Once installed, the source generators will automatically run during your project's build process. No additional configuration is required.

The generated code will be available in your project's namespace and can be used immediately in your code.

## Requirements

- **.NET 8.0** or later (for projects using the generators)
- **C# 11** or later

## Documentation

For more information about the BbQ.Outcome library and its features, visit the [main repository](https://github.com/JeanMarcMbouma/Outcome).

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request to the [main repository](https://github.com/JeanMarcMbouma/Outcome).
