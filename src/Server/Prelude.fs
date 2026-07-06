[<AutoOpen>]
module AssetTracker.Server.Prelude

/// Under nullness checking, Operators.box returns `objnull`. Boxing a value we
/// hold is never null, so narrow it back — keeps ADO parameter plumbing quiet
/// without sprinkling `nonNull` at every call site.
let inline box (x: 'T) : obj = nonNull (Operators.box x)
