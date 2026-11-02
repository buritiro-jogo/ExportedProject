using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FreeBala
{
public static class FBMediaLib
{
	private static readonly string[] EXT = new string[9] { ".mp3", ".ogg", ".wav", ".m4a", ".aac", ".aif", ".aiff", ".flac", ".webm" };

	private static readonly Dictionary<string, FBTrack> byPath = new Dictionary<string, FBTrack>();

	public static List<FBTrack> lib = new List<FBTrack>();

	public static string Dir => Path.Combine(Application.persistentDataPath, "Musicas");

	public static string Received => Path.Combine(Dir, "Recebidas");

	public static IEnumerable<string> Roots()
	{
		yield return Dir;
		string text = null;
		try
		{
			text = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
		}
		catch
		{
		}
		if (!string.IsNullOrEmpty(text))
		{
			yield return text;
		}
	}

	public static string Mime(string ext)
	{
		return ext switch
		{
			".mp3" => "audio/mpeg", 
			".ogg" => "audio/ogg", 
			".wav" => "audio/wav", 
			".m4a" => "audio/mp4", 
			".aac" => "audio/aac", 
			".flac" => "audio/flac", 
			".webm" => "audio/webm", 
			_ => "audio/aiff", 
		};
	}

	public static string Ext(string mime)
	{
		return mime switch
		{
			"audio/mpeg" => ".mp3", 
			"audio/ogg" => ".ogg", 
			"audio/wav" => ".wav", 
			"audio/x-wav" => ".wav", 
			"audio/mp4" => ".m4a", 
			"audio/aac" => ".aac", 
			"audio/flac" => ".flac", 
			"audio/webm" => ".webm", 
			"video/mp4" => ".mp4", 
			"video/webm" => ".webm", 
			_ => ".mp3", 
		};
	}

	public static void Refresh()
	{
		try
		{
			Directory.CreateDirectory(Dir);
		}
		catch
		{
		}
		List<FBTrack> list = new List<FBTrack>();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (string item in Roots())
		{
			if (!Directory.Exists(item))
			{
				continue;
			}
			IEnumerable<string> enumerable;
			try
			{
				enumerable = (from f in Directory.EnumerateFiles(item, "*.*", SearchOption.AllDirectories)
					where EXT.Contains(Path.GetExtension(f).ToLowerInvariant())
					select f).Take(3000).ToList();
			}
			catch
			{
				continue;
			}
			foreach (string item2 in enumerable)
			{
				try
				{
					FileInfo fileInfo = new FileInfo(item2);
					string key = item2 + "|" + fileInfo.Length + "|" + fileInfo.LastWriteTimeUtc.Ticks;
					if (!byPath.TryGetValue(key, out var value))
					{
						value = Read(item2, fileInfo);
						byPath[key] = value;
					}
					if (value != null && hashSet.Add(value.h))
					{
						list.Add(value);
					}
				}
				catch
				{
				}
			}
		}
		lib = (from t in list
			orderby (t.folder == "Recebidas") ? 1 : 0, t.a, t.n
			select t).ToList();
	}

	private static FBTrack Read(string f, FileInfo fi)
	{
		string text = Path.GetExtension(f).ToLowerInvariant();
		FBTrack fBTrack = new FBTrack
		{
			path = f,
			s = fi.Length,
			m = Mime(text),
			h = TrackId(f, fi.Length)
		};
		string fileName = Path.GetFileName(Path.GetDirectoryName(f));
		fBTrack.folder = (string.Equals(fileName, "Recebidas", StringComparison.OrdinalIgnoreCase) ? "Recebidas" : fileName);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(f);
		string[] array = fileNameWithoutExtension.Split(new string[1] { " - " }, 2, StringSplitOptions.None);
		if (array.Length == 2)
		{
			fBTrack.a = array[0].Trim();
			fBTrack.n = array[1].Trim();
		}
		else
		{
			fBTrack.n = fileNameWithoutExtension;
			fBTrack.a = "";
		}
		if (text == ".mp3")
		{
			try
			{
				Id3(f, fBTrack);
			}
			catch
			{
			}
		}
		return fBTrack;
	}

	public static string TrackId(string f, long size)
	{
		FileStream fs = File.OpenRead(f);
		try
		{
			return TrackId(delegate((long off, int len) o)
			{
				fs.Position = o.off;
				byte[] array = new byte[o.len];
				int num;
				for (int i = 0; i < o.len; i += num)
				{
					num = fs.Read(array, i, o.len - i);
					if (num <= 0)
					{
						break;
					}
				}
				return array;
			}, size);
		}
		finally
		{
			if (fs != null)
			{
				((IDisposable)fs).Dispose();
			}
		}
	}

	public static string TrackId(byte[] all)
	{
		return TrackId(delegate((long off, int len) o)
		{
			byte[] array = new byte[o.len];
			Buffer.BlockCopy(all, (int)o.off, array, 0, o.len);
			return array;
		}, all.Length);
	}

	private static string TrackId(Func<(long off, int len), byte[]> read, long size)
	{
		byte[] array = read((0L, (int)Math.Min(262144L, size)));
		long num = Math.Max(0L, size - 65536);
		byte[] array2 = read((num, (int)(size - num)));
		byte[] array3 = new byte[array.Length + array2.Length + 8];
		Buffer.BlockCopy(array, 0, array3, 0, array.Length);
		Buffer.BlockCopy(array2, 0, array3, array.Length, array2.Length);
		byte[] bytes = BitConverter.GetBytes((double)size);
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(bytes);
		}
		Buffer.BlockCopy(bytes, 0, array3, array.Length + array2.Length, 8);
		using SHA1 sHA = SHA1.Create();
		byte[] array4 = sHA.ComputeHash(array3);
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < 10; i++)
		{
			stringBuilder.Append(array4[i].ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	private static void Id3(string f, FBTrack t)
	{
		using FileStream fileStream = File.OpenRead(f);
		byte[] array = new byte[10];
		if (fileStream.Read(array, 0, 10) < 10 || array[0] != 73 || array[1] != 68 || array[2] != 51)
		{
			return;
		}
		int num = array[3];
		int num2 = (array[6] << 21) | (array[7] << 14) | (array[8] << 7) | array[9];
		if (num2 <= 0 || num2 > 4194304)
		{
			num2 = Math.Min(num2, 4194304);
		}
		byte[] array2 = new byte[Math.Min(num2, 262144)];
		fileStream.Read(array2, 0, array2.Length);
		int num3;
		for (int i = 0; i + 10 <= array2.Length; i += 10 + num3)
		{
			string @string = Encoding.ASCII.GetString(array2, i, 4);
			if (@string[0] == '\0')
			{
				break;
			}
			num3 = ((num >= 4) ? ((array2[i + 4] << 21) | (array2[i + 5] << 14) | (array2[i + 6] << 7) | array2[i + 7]) : ((array2[i + 4] << 24) | (array2[i + 5] << 16) | (array2[i + 6] << 8) | array2[i + 7]));
			if (num3 <= 0 || i + 10 + num3 > array2.Length)
			{
				break;
			}
			if (!(@string == "TIT2") && !(@string == "TPE1"))
			{
				continue;
			}
			int num4 = array2[i + 10];
			byte[] array3 = new byte[num3 - 1];
			Buffer.BlockCopy(array2, i + 11, array3, 0, array3.Length);
			string text = (num4 switch
			{
				1 => Encoding.Unicode.GetString((array3.Length >= 2 && array3[0] == 254) ? array3.Skip(2).ToArray() : array3.Skip((array3.Length >= 2 && array3[0] == 255) ? 2 : 0).ToArray()), 
				2 => Encoding.BigEndianUnicode.GetString(array3), 
				3 => Encoding.UTF8.GetString(array3), 
				_ => Encoding.GetEncoding("ISO-8859-1").GetString(array3), 
			}).Trim('\0', ' ');
			if (text.Length > 0)
			{
				if (@string == "TIT2")
				{
					t.n = text;
				}
				else
				{
					t.a = text;
				}
			}
		}
	}

	public static FBTrack Find(string h)
	{
		if (h != null)
		{
			return lib.FirstOrDefault((FBTrack t) => t.h == h);
		}
		return null;
	}

	public static FBTrack AddReceived(string h, byte[] data, FBTrack meta)
	{
		Directory.CreateDirectory(Received);
		string text = string.Concat(((string.IsNullOrEmpty(meta.a) ? "" : (meta.a + " - ")) + (meta.n ?? h)).Select((char c) => (!Path.GetInvalidFileNameChars().Contains(c)) ? c : '_'));
		string text2 = Path.Combine(Received, text + Ext(meta.m));
		File.WriteAllBytes(text2, data);
		FBTrack fBTrack = new FBTrack
		{
			h = h,
			n = meta.n,
			a = meta.a,
			d = meta.d,
			m = meta.m,
			s = data.Length,
			path = text2,
			folder = "Recebidas"
		};
		byPath[text2 + "|" + data.Length + "|" + new FileInfo(text2).LastWriteTimeUtc.Ticks] = fBTrack;
		lib.RemoveAll((FBTrack x) => x.h == h);
		lib.Add(fBTrack);
		return fBTrack;
	}

	public static AudioType TypeOf(string path)
	{
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text != null)
		{
			int length = text.Length;
			if (length != 4)
			{
				if (length == 5 && text == ".aiff")
				{
					goto IL_0101;
				}
			}
			else
			{
				switch (text[3])
				{
				case '3':
					if (!(text == ".mp3"))
					{
						goto IL_0109;
					}
					return AudioType.MPEG;
				case 'g':
					if (!(text == ".ogg"))
					{
						goto IL_0109;
					}
					return AudioType.OGGVORBIS;
				case 'v':
					if (!(text == ".wav"))
					{
						goto IL_0109;
					}
					return AudioType.WAV;
				case 'f':
					break;
				case 'a':
					if (text == ".m4a")
					{
						goto IL_0105;
					}
					goto IL_0109;
				case 'c':
					if (text == ".aac")
					{
						goto IL_0105;
					}
					goto IL_0109;
				case '4':
					if (text == ".mp4")
					{
						goto IL_0105;
					}
					goto IL_0109;
				default:
					goto IL_0109;
					IL_0105:
					return AudioType.ACC;
				}
				if (text == ".aif")
				{
					goto IL_0101;
				}
			}
		}
		goto IL_0109;
		IL_0101:
		return AudioType.AIFF;
		IL_0109:
		return AudioType.UNKNOWN;
	}
}
}
