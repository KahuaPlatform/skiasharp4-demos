global using System.Collections.Immutable;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using ApplicationExecutionState = Windows.ApplicationModel.Activation.ApplicationExecutionState;

// Shared neon chassis lives in Source/Common/ and is pulled in by every demo
// via a Compile Include in the csproj. These global usings let game code refer
// to Vec2, HighScoreStore, etc. unqualified.
global using Arcade.Common;
global using Arcade.Common.Audio;
global using Arcade.Common.Chassis;
