# iOS Build Number Plugin

This plugin retrieves the iOS build number (CFBundleVersion) from Info.plist at runtime.

## How It Works

The plugin consists of two parts:

1. **C# Wrapper** (`iOSBuildNumber.cs`): Provides a C# interface to call the native iOS code
2. **Native Implementation** (`LvlUpBuildNumber.mm`): Objective-C++ code that reads CFBundleVersion from Info.plist

## Usage

```csharp
string buildNumber = LvlUp.Plugins.iOSBuildNumber.GetBuildNumber();
```

The method returns:
- The build number string on iOS devices
- `null` when running in Unity Editor or on non-iOS platforms

## Setting Build Number in Unity

To set the build number that will be read by this plugin:

1. Open Unity Project Settings
2. Go to **Player** > **iOS** > **Other Settings**
3. Set the **Build** number (this becomes CFBundleVersion in Info.plist)
4. Note: The **Version** field becomes CFBundleShortVersionString

## Technical Details

- The native code uses `[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"]`
- The `.mm` file is configured to only compile for iOS builds (see .meta file)
- Memory is properly allocated and managed for the string return value
- The C# wrapper uses `[DllImport("__Internal")]` to link with the native code

## Integration

This plugin is automatically used by `LvlUpEvent` when tracking events on iOS devices. The build number is captured in the `appBuild` field of event metadata.

