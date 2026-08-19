# Common.Tests

MSTest coverage for the shared neon chassis in `Source/Common/`.

The chassis is source-included here with the same `<Compile>` glob the demos use, so these tests
exercise the same compilation the games get. `AmbientStarBackdrop.cs` is the only exclusion — it
derives from Uno's `SKCanvasElement` and is the sole chassis file with a UI dependency.

Run with `.\Builds\Test-Common.ps1`, or as part of `.\Builds\Test-All.ps1`.

These are the tests [08 – Chassis Extensions](../../Docs/Architecture/08-Chassis-Extensions.md)
records as "planned, never written" — the camera seam maths especially, which it called out as
"the easiest thing to get subtly wrong".
