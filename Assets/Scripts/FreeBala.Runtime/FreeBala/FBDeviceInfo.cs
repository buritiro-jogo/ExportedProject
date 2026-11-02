using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public struct FBDeviceInfo
{
	public FBPlat plat;

	public bool mobileBrowser;

	public int memMB;

	public int gpuMB;

	public int cores;

	public int shaderLevel;

	public string gpu;

	public GraphicsDeviceType api;

	public bool Mobile
	{
		get
		{
			if (plat != FBPlat.Android && plat != FBPlat.iOS)
			{
				return mobileBrowser;
			}
			return true;
		}
	}

	public static FBDeviceInfo Current()
	{
		FBPlat fBPlat = Application.platform switch
		{
			RuntimePlatform.WebGLPlayer => FBPlat.WebGL, 
			RuntimePlatform.IPhonePlayer => FBPlat.iOS, 
			RuntimePlatform.Android => FBPlat.Android, 
			_ => FBPlat.PC, 
		};
		FBDeviceInfo result = default(FBDeviceInfo);
		result.plat = fBPlat;
		result.mobileBrowser = fBPlat == FBPlat.WebGL && Application.isMobilePlatform;
		result.memMB = SystemInfo.systemMemorySize;
		result.gpuMB = SystemInfo.graphicsMemorySize;
		result.cores = SystemInfo.processorCount;
		result.shaderLevel = SystemInfo.graphicsShaderLevel;
		result.gpu = SystemInfo.graphicsDeviceName ?? "";
		result.api = SystemInfo.graphicsDeviceType;
		return result;
	}
}
}
