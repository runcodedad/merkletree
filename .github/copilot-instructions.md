# Copilot Instructions (concise)

Purpose: short, direct guidelines for contributors and Copilot suggestions.

## Project layout
- Single `MerkleTree` project with logical organization: `Core/`, `Hashing/`, `Proofs/`, `Cache/`, `Serialization/`.

## XML Documentation
- Always document all public APIs with `<summary>`, `<param>`, `<returns>`, `<exception>`
- Always include usage examples for complex APIs with `<example>` and `<code>`

## Markdown Documentation
- Keep main `README.md` as a general overview and directory to specific docs
- Never include implementation specifics in main `README.md`
- Always create project-specific `README.md` for each `MerkleTree.*` project in `src/`
- Always include architecture, usage examples, and API documentation in project READMEs
- Never create READMEs for test projects (`*.Tests`)
- Always place non-project-specific documentation in `docs/` folder
- Use clear, concise language with code examples where appropriate

## Required Coding Patterns

### Naming
- Use PascalCase for classes, methods, properties
- Use _camelCase for private fields
- Use interfaces starting with `I` (e.g., `IHashFunction`)

### Nullable Types
- Always enable nullable reference types: `<Nullable>enable</Nullable>`
- Use `ArgumentNullException.ThrowIfNull(parameter)` for null checks
- Never use `!` null-forgiving operator

### Async/Await
- Always use async for I/O operations (file, network, database)
- Always suffix async methods with `Async`
- Always return `Task` or `Task<T>`
- Always accept `CancellationToken` as last parameter with default value
- Always check `cancellationToken.ThrowIfCancellationRequested()` in loops

### Dependency Injection
- Always use constructor injection
- Always inject interfaces, never concrete classes
- Always validate dependencies in constructor with `ArgumentNullException.ThrowIfNull`

### Immutability
- Always use `readonly` fields
- Always use records for data classes
- Always use `ReadOnlyMemory<byte>` for binary data (keys, seeds)
- Always use `ReadOnlySpan<T>` or `IReadOnlyList<T>` for exposing collections

### Error Handling
- Always use specific exceptions: `ArgumentNullException`, `ArgumentException`, `InvalidOperationException`, `IOException`
- Always validate input early
- Never throw generic `Exception`
- Never catch exceptions you can't handle

## Testing
- Use xUnit, NSubstitute for mocking, and place tests under `tests/MerkleTree.Tests/`
- Always follow Arrange-Act-Assert pattern
- Always name tests: `MethodName_Scenario_ExpectedBehavior`
- Always clean up resources (files, streams) with `try/finally` or `using`
- Always target 90%+ code coverage for business logic
- Always mock interfaces only, never concrete classes

## Security Requirements
- Always validate all external inputs before processing
- Always check size limits, value ranges, format correctness
- Never log private keys, passwords, or sensitive data
- Never expose stack traces to external clients
- Never store private keys in plain text
- Always encrypt private keys with user passphrase
- Always copy byte arrays for sensitive data to prevent external modification

## Forbidden Patterns
- Never use blocking I/O: `File.ReadAllBytes`, `stream.Read` without async
- Never use `.Result` or `.Wait()` on async operations
- Never access file system directly without abstraction (use `IFileSystem`)
- Never hardcode configuration values (use config classes with `init` properties)
- Never use mutable static state
- Never use `async void` (except event handlers)
- Never ignore `CancellationToken` parameters
- Never optimize prematurely (profile first)

## Required Architecture
- Use Factory pattern for complex object creation
- Use Repository pattern with interfaces for storage
- Use Observer pattern for event notifications
- Use Disposable pattern (`IAsyncDisposable`) for file handles and network connections

## Multi-targeting
- Target NET10_0 and NETStandard2.1; use `#if NET10_0` for platform-specific code.

## Merkle Tree Specific Rules

### Hash algorithms
- Default to SHA-256. Support SHA-512. BLAKE3 is available only for NET10_0 builds.

### Performance
- Use streaming APIs for large datasets (`MerkleTreeStream`). Cache top levels when generating many proofs.

### Binary Data
- Always use binary serialization for plots, proofs, blocks
- Always use `BinaryWriter`/`BinaryReader` for simple structures
- Always specify little-endian explicitly
- Always version serialization formats

### Byte Order (Endianness)
- Always use `BinaryPrimitives` from `System.Buffers.Binary` for reading/writing numeric types
- Always specify little-endian explicitly: `WriteInt64LittleEndian`, `ReadInt32LittleEndian`, etc.
- Never use `BitConverter` (platform-dependent endianness)
- Always ensure cross-platform compatibility for binary formats (plots, blocks, network messages)
- Always use little-endian for all serialized numeric data
