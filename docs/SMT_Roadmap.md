# Sparse Merkle Tree (SMT) — Roadmap

This roadmap outlines a minimal, dependency-aware implementation path for the SMT core library. It is intentionally simple and focused on delivering a clean, storage-agnostic core that supports deterministic hashing, proofs, and pluggable persistence adapters.

## Guiding principles
- Storage-agnostic: core contains no DB, filesystem, or blockchain logic.
- Deterministic: identical inputs produce identical roots across platforms.
- Pluggable: hashing and persistence are abstracted and injectable.
- Minimal persistence surface: core returns nodes-to-persist but never persists directly.

## High-level phases (recommended order)
1. Foundation — Hashing & Metadata
   - Implement hash algorithm abstraction and domain-separated hashing primitives.
     - Tracked in issue: #39
   - Define metadata and zero-hash generation utilities.
     - Tracked in issue: #43
   - Deliverables:
     - HashAlgorithm interface (SHA-256 default)
     - Deterministic zero-hash table generator
   - Why first: hashing and metadata are prerequisites for bit-path derivation and proof verification.

2. Interfaces & Model
   - Define persistence abstraction interfaces (read/write, batch, snapshots, metadata).
     - Tracked in issue: #42
   - Implement the SMT tree model (key → bit-path mapping, node types, depth config).
     - Tracked in issue: #38
   - Deliverables:
     - Persistence interfaces (language-idiomatic)
     - Tree model APIs and bit-path utilities
   - Why next: core operations and tests depend on these abstractions.

3. Core Operations & Errors
   - Implement Get / Update / Delete and deterministic batch updates (return nodes-to-persist).
     - (Create issue for tracking if desired)
   - Define a consistent error model for verification, depth mismatch, and adapter failures.
     - Tracked in issue: #44
   - Deliverables:
     - Deterministic batch semantics (documented)
     - Typed error classes/codes
   - Why now: operations require model, hashing, and persistence interfaces.

4. Proofs & Verification
   - Implement inclusion and non-inclusion proofs, optional compression, and verification routines.
     - Tracked in issue: #41
   - Deliverables:
     - Proof generation APIs (inclusion/non-inclusion)
     - Proof compression (omit canonical zeros) + bitmask
     - Verification utility: verify(root, key, proof) -> valid/invalid with error reasons

5. Testing, Reference Adapter, CI
   - Add an in-memory reference adapter and a test suite: determinism, proof correctness, property tests.
     - (Create issue for tracking if desired)
   - Integrate tests into CI, include test vectors and property tests.

6. Documentation & Constraints Enforcement
   - Add CONTRIBUTING.md and CI/lint checks to enforce "no DB / no filesystem / no blockchain" rules.
     - Tracked in issue: #46
   - Deliverables:
     - CONTRIBUTING guidance
     - CI check(s) for banned imports/usages

## Quick timeline (example)
- Foundation (Phase 1): 1–2 sprints
- Interfaces & Model (Phase 2): 1–2 sprints
- Core Ops & Errors (Phase 3): 2–3 sprints
- Proofs & Verification (Phase 4): 2–3 sprints
- Tests & CI (Phase 5): ongoing, start concurrently with Phase 2
Adjust per team size and desired depth of test coverage.

## How to use this roadmap
- Start by assigning or working on the tracked issues in the phase order above.
- Create explicit issues for "Core Operations" and "Testing & Reference Adapter" if you want them tracked in the repository.
- Use the in-memory adapter (from testing phase) to validate adapter implementations.
- Keep the metadata/hash primitives stable — changes here are breaking.

## References
- #39 — Hash abstraction & domain-separated hashing  
- #43 — Metadata structure & zero-hash table  
- #42 — Persistence abstraction interfaces  
- #38 — Tree model & key → bit-path mapping  
- #44 — Error handling model  
- #41 — Proof generation & verification  
- #46 — Documentation & constraints enforcement

## Notes
- This roadmap is intentionally pragmatic and conservative: implement core foundations first, then build operations and proofs.
- If you want I can turn this into a milestone with linked issues and/or create the missing issues for Core Operations and Testing.
