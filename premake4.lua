solution "MonoGame.Framework.Windows.Premake"
  configurations { "Debug", "Release" }
  platforms { "Native" }
  
project "MonoGame.Framework.Windows.Premake"
  location "MonoGame.Framework"
  kind "SharedLib"
  language "C#"
  uuid "7DE47032-A904-4C29-BD22-2D235E8D91BA"
  defines { "WINDOWS", "DIRECTX", "WINDOWS_MEDIA_SESSION" }
  flags { "Unsafe" }
  configuration "Debug"
    defines { "DEBUG", "TRACE" }
    flags { "Symbols" }
  configuration "Release"
    flags { "Optimize" }
  files
  {
    "MonoGame.Framework/*.cs",
    "MonoGame.Framework/Audio/*.cs",
    "MonoGame.Framework/Content/**.cs",
    "MonoGame.Framework/GamerServices/*.cs",
    "MonoGame.Framework/Graphics/**.cs",
    "MonoGame.Framework/Graphics/Effect/Resources/*.dx11.mgfxo",
    "MonoGame.Framework/Input/**.cs",
    "MonoGame.Framework/Media/*.cs",
    "MonoGame.Framework/Storage/*.cs",
    "MonoGame.Framework/Utilities/*.cs",
    "MonoGame.Framework/Windows/*.cs",
    "MonoGame.Framework/Windows8/GamePad.cs",
    "MonoGame.Framework/Windows8/SharpDXHelper.cs",
    "Properties/AssemblyInfo.cs"
  }
  excludes
  {
    "MonoGame.Framework/GamerServices/MonoLive*.cs"
  }
  links
  {
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.dll",
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.Direct2D1.dll",
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.Direct3D11.dll",
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.DXGI.dll",
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.MediaFoundation.dll",
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.XAudio2.dll",
    "../ThirdParty/Libs/SharpDX/Windows/SharpDX.XInput.dll",
    "System",
    "System.Core",
    "System.Runtime.Serialization",
    "System.Web",
    "System.Web.Services",
    "System.Windows.Forms",
    "System.Xml.Linq",
    "System.Xml",
    "System.Drawing"
    }
    