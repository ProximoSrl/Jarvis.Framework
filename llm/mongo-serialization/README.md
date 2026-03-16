# Mongo Serialization Overview

Use this page before opening framework code when the task involves Mongo
serialization, discriminators, or message registration.

## Mental model

Jarvis.Framework expects identity values that implement `IIdentity` to be
stored as flat strings in Mongo, not as nested objects.

Messages, commands, and domain events also rely on custom discriminator
registration so Mongo can deserialize the correct CLR type.

## Critical startup order

1. Enable flat identity mapping first with `MongoFlatMapper.EnableFlatMapping()`.
2. Only after that, register message assemblies with
   `MessagesRegistration.RegisterAssembly(...)`.

If the order is reversed, Mongo can persist data with the wrong shape or the
wrong discriminator naming convention.

## Common failure modes

- Unknown discriminator errors because one component saved documents without
  the expected alias/version naming.
- Id fields stored as nested documents like `{ Id: "Document_2" }` instead of
  flat strings like `"Document_2"`.
- Commands or events not being deserialized because their assembly was not
  registered.

## Read next in code

- `Jarvis.Framework.Shared/IdentitySupport/MongoFlatMapper.cs`
- `Jarvis.Framework/Support/MongoRegistration.cs`

## Practical rule

If persistence errors mention discriminators, unexpected document shape, or
Mongo string conversion, inspect flat mapping and assembly registration before
changing domain code.
