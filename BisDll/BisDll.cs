using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using BisDll.Common;
using BisDll.Common.Math;
using BisDll.Compression;
using BisDll.Model.MLOD;
using BisDll.Model.ODOL;
using BisDll.Stream;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("BisDll")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("BisDll")]
[assembly: AssemblyCopyright("Copyright ©  2016")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("c835a5b0-6425-4c8e-8e59-87b9af8576a0")]
[assembly: AssemblyFileVersion("1.5.*")]
[assembly: TargetFramework(".NETFramework,Version=v4.6.1", FrameworkDisplayName = ".NET Framework 4.6.1")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.5.6230.1995")]
[module: UnverifiableCode]
namespace BisDll
{
	public static class Methods
	{
		public static void Swap<T>(ref T v1, ref T v2)
		{
			T val = v1;
			v1 = v2;
			v2 = val;
		}

		public static bool EqualsFloat(float f1, float f2, float tolerance = 0.0001f)
		{
			if (Math.Abs(f1 - f2) <= tolerance)
			{
				return true;
			}
			return false;
		}

		public static IEnumerable<T> Yield<T>(this T src)
		{
			yield return src;
		}

		public static IEnumerable<T> Yield<T>(params T[] elems)
		{
			return elems;
		}

		public static string CharsToString(this IEnumerable<char> chars)
		{
			return new string(chars.ToArray());
		}
	}
}
namespace BisDll.Stream
{
	public interface IDeserializable
	{
		void ReadObject(BinaryReaderEx input);
	}
	public class BinaryReaderEx : BinaryReader
	{
		public bool UseCompressionFlag { get; set; }

		public bool UseLZOCompression { get; set; }

		public int Version { get; set; }

		public long Position
		{
			get
			{
				return BaseStream.Position;
			}
			set
			{
				BaseStream.Position = value;
			}
		}

		public BinaryReaderEx(System.IO.Stream stream)
			: base(stream)
		{
			UseCompressionFlag = false;
		}

		public uint ReadUInt24()
		{
			return (uint)(ReadByte() + (ReadByte() << 8) + (ReadByte() << 16));
		}

		public string ReadAscii(int count)
		{
			string text = "";
			for (int i = 0; i < count; i++)
			{
				text += (char)ReadByte();
			}
			return text;
		}

		public string ReadAsciiz()
		{
			string text = "";
			char c;
			while ((c = (char)ReadByte()) != 0)
			{
				text += c;
			}
			return text;
		}

		private T ReadObject<T>() where T : IDeserializable, new()
		{
			T result = new T();
			result.ReadObject(this);
			return result;
		}

		private T[] ReadArrayBase<T>(Func<BinaryReaderEx, T> readElement, int size)
		{
			T[] array = new T[size];
			for (int i = 0; i < size; i++)
			{
				array[i] = readElement(this);
			}
			return array;
		}

		public T[] ReadArray<T>(Func<BinaryReaderEx, T> readElement)
		{
			return ReadArrayBase(readElement, ReadInt32());
		}

		public T[] ReadArray<T>() where T : IDeserializable, new()
		{
			return ReadArray((BinaryReaderEx i) => i.ReadObject<T>());
		}

		public float[] ReadFloatArray()
		{
			return ReadArray((BinaryReaderEx i) => i.ReadSingle());
		}

		public int[] ReadIntArray()
		{
			return ReadArray((BinaryReaderEx i) => i.ReadInt32());
		}

		public string[] ReadStringArray()
		{
			return ReadArray((BinaryReaderEx i) => i.ReadAsciiz());
		}

		public T[] ReadCompressedArray<T>(Func<BinaryReaderEx, T> readElement, int elemSize)
		{
			int num = ReadInt32();
			uint expectedSize = (uint)(num * elemSize);
			return new BinaryReaderEx(new MemoryStream(ReadCompressed(expectedSize))).ReadArrayBase(readElement, num);
		}

		public T[] ReadCompressedArray<T>(Func<BinaryReaderEx, T> readElement)
		{
			return ReadCompressedArray(readElement, Marshal.SizeOf(typeof(T)));
		}

		public T[] ReadCompressedObjectArray<T>(int sizeOfT) where T : IDeserializable, new()
		{
			return ReadCompressedArray((BinaryReaderEx i) => i.ReadObject<T>(), sizeOfT);
		}

		public short[] ReadCompressedShortArray()
		{
			return ReadCompressedArray((BinaryReaderEx i) => i.ReadInt16());
		}

		public int[] ReadCompressedIntArray()
		{
			return ReadCompressedArray((BinaryReaderEx i) => i.ReadInt32());
		}

		public float[] ReadCompressedFloatArray()
		{
			return ReadCompressedArray((BinaryReaderEx i) => i.ReadSingle());
		}

		public T[] ReadCondensedArray<T>(Func<BinaryReaderEx, T> readElement, int sizeOfT)
		{
			int num = ReadInt32();
			T[] array = new T[num];
			if (ReadBoolean())
			{
				T val = readElement(this);
				for (int i = 0; i < num; i++)
				{
					array[i] = val;
				}
				return array;
			}
			uint expectedSize = (uint)(num * sizeOfT);
			BinaryReaderEx binaryReaderEx = new BinaryReaderEx(new MemoryStream(ReadCompressed(expectedSize)));
			array = binaryReaderEx.ReadArrayBase(readElement, num);
			binaryReaderEx.Close();
			return array;
		}

		public T[] ReadCondensedObjectArray<T>(int sizeOfT) where T : IDeserializable, new()
		{
			return ReadCondensedArray((BinaryReaderEx i) => i.ReadObject<T>(), sizeOfT);
		}

		public int[] ReadCondensedIntArray()
		{
			return ReadCondensedArray((BinaryReaderEx i) => i.ReadInt32(), 4);
		}

		public int ReadCompactInteger()
		{
			int num = ReadByte();
			if ((num & 0x80) != 0)
			{
				int num2 = ReadByte();
				num += (num2 - 1) * 128;
			}
			return num;
		}

		public byte[] ReadCompressed(uint expectedSize)
		{
			if (expectedSize == 0)
			{
				return new byte[0];
			}
			if (UseLZOCompression)
			{
				return ReadLZO(expectedSize);
			}
			return ReadLZSS(expectedSize);
		}

		public byte[] ReadLZO(uint expectedSize)
		{
			bool flag = expectedSize >= 1024;
			if (UseCompressionFlag)
			{
				flag = ReadBoolean();
			}
			if (!flag)
			{
				return ReadBytes((int)expectedSize);
			}
			return LZO.readLZO(BaseStream, expectedSize);
		}

		public byte[] ReadLZSS(uint expectedSize, bool inPAA = false)
		{
			if (expectedSize < 1024 && !inPAA)
			{
				return ReadBytes((int)expectedSize);
			}
			byte[] dst = new byte[expectedSize];
			LZSS.readLZSS(BaseStream, out dst, expectedSize, inPAA);
			return dst;
		}

		public byte[] ReadCompressedIndices(int bytesToRead, uint expectedSize)
		{
			byte[] array = new byte[expectedSize];
			int num = 0;
			for (int i = 0; i < bytesToRead; i++)
			{
				byte b = ReadByte();
				if ((b & 0x80) != 0)
				{
					byte b2 = (byte)(b - 127);
					byte b3 = ReadByte();
					for (int j = 0; j < b2; j++)
					{
						array[num++] = b3;
					}
				}
				else
				{
					for (int k = 0; k < b + 1; k++)
					{
						array[num++] = ReadByte();
					}
				}
			}
			return array;
		}

		public uint skipGridCompressed()
		{
			long position = Position;
			ushort num = ReadUInt16();
			for (int i = 0; i < 16; i++)
			{
				if ((num & 1) == 1)
				{
					skipGridCompressed();
				}
				else
				{
					Position += 4L;
				}
				num >>= 1;
			}
			return (uint)(Position - position);
		}
	}
	public class BinaryWriter : System.IO.BinaryWriter
	{
		public long Position
		{
			get
			{
				return BaseStream.Position;
			}
			set
			{
				BaseStream.Position = value;
			}
		}

		public BinaryWriter(System.IO.Stream dstStream)
			: base(dstStream)
		{
		}

		public void writeAscii(string text, uint len)
		{
			Write(text.ToCharArray());
			uint num = (uint)(len - text.Length);
			for (int i = 0; i < num; i++)
			{
				Write('\0');
			}
		}

		public void writeAsciiz(string text)
		{
			Write(text.ToCharArray());
			Write('\0');
		}
	}
}
namespace BisDll.Model
{
	public static class Conversion
	{
		private struct PointWeight
		{
			public int pointIndex;

			public byte weight;

			public PointWeight(int index, byte weight)
			{
				pointIndex = index;
				this.weight = weight;
			}
		}

		private static PointFlags clipFlagsToPointFlags(ClipFlags clipFlags)
		{
			PointFlags pointFlags = PointFlags.NONE;
			if ((clipFlags & ClipFlags.ClipLandStep) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.ONLAND;
			}
			else if ((clipFlags & ClipFlags.ClipLandUnder) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.UNDERLAND;
			}
			else if ((clipFlags & ClipFlags.ClipLandAbove) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.ABOVELAND;
			}
			else if ((clipFlags & ClipFlags.ClipLandKeep) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.KEEPLAND;
			}
			if ((clipFlags & ClipFlags.ClipDecalStep) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.DECAL;
			}
			else if ((clipFlags & ClipFlags.ClipDecalVertical) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.VDECAL;
			}
			if ((clipFlags & (ClipFlags)209715200) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.NOLIGHT;
			}
			else if ((clipFlags & (ClipFlags)212860928) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.FULLLIGHT;
			}
			else if ((clipFlags & (ClipFlags)211812352) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.HALFLIGHT;
			}
			else if ((clipFlags & (ClipFlags)210763776) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.AMBIENT;
			}
			if ((clipFlags & ClipFlags.ClipFogStep) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.NOFOG;
			}
			else if ((clipFlags & ClipFlags.ClipFogSky) != ClipFlags.ClipNone)
			{
				pointFlags |= PointFlags.SKYFOG;
			}
			int num = (int)(clipFlags & ClipFlags.ClipUserMask) / 1048576;
			return (PointFlags)((uint)pointFlags | (uint)(65536 * num));
		}

		public static BisDll.Model.MLOD.MLOD ODOL2MLOD(BisDll.Model.ODOL.ODOL odol)
		{
			P3D_LOD[] lODs = odol.LODs;
			int num = lODs.Length;
			MLOD_LOD[] array = new MLOD_LOD[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = OdolLod2MLOD(odol, (LOD)lODs[i]);
			}
			return new BisDll.Model.MLOD.MLOD(array);
		}

		private static MLOD_LOD OdolLod2MLOD(BisDll.Model.ODOL.ODOL odol, LOD src)
		{
			MLOD_LOD mLOD_LOD = new MLOD_LOD(src.Resolution);
			int vertexCount = src.VertexCount;
			ConvertPoints(odol, mLOD_LOD, src);
			mLOD_LOD.normals = src.Normals;
			ConvertFaces(odol, mLOD_LOD, src);
			float mass = odol.modelInfo.mass;
			_ = odol.Skeleton;
			mLOD_LOD.taggs = new List<Tagg>();
			if (src.Resolution == 1E+13f)
			{
				MassTagg item = createMassTagg(vertexCount, mass);
				mLOD_LOD.taggs.Add(item);
			}
			IEnumerable<UVSetTagg> collection = createUVSetTaggs(src);
			mLOD_LOD.taggs.AddRange(collection);
			IEnumerable<PropertyTagg> collection2 = createPropertyTaggs(src);
			mLOD_LOD.taggs.AddRange(collection2);
			IEnumerable<NamedSelectionTagg> collection3 = createNamedSelectionTaggs(src);
			mLOD_LOD.taggs.AddRange(collection3);
			IEnumerable<AnimationTagg> collection4 = createAnimTaggs(src);
			mLOD_LOD.taggs.AddRange(collection4);
			if (Resolution.KeepsNamedSelections(src.Resolution))
			{
				return mLOD_LOD;
			}
			Dictionary<string, List<PointWeight>> points = new Dictionary<string, List<PointWeight>>();
			Dictionary<string, List<int>> faces = new Dictionary<string, List<int>>();
			ReconstructNamedSelectionBySections(src, out points, out faces);
			Dictionary<string, List<PointWeight>> points2 = new Dictionary<string, List<PointWeight>>();
			Dictionary<string, List<int>> faces2 = new Dictionary<string, List<int>>();
			ReconstructProxies(src, out points2, out faces2);
			Dictionary<string, List<PointWeight>> points3 = new Dictionary<string, List<PointWeight>>();
			ReconstructNamedSelectionsByBones(src, odol.Skeleton, out points3);
			ApplySelectedPointsAndFaces(mLOD_LOD, points, faces);
			ApplySelectedPointsAndFaces(mLOD_LOD, points2, faces2);
			ApplySelectedPointsAndFaces(mLOD_LOD, points3, null);
			return mLOD_LOD;
		}

		private static void ApplySelectedPointsAndFaces(MLOD_LOD dstLod, Dictionary<string, List<PointWeight>> nsPoints, Dictionary<string, List<int>> nsFaces)
		{
			foreach (Tagg tagg in dstLod.taggs)
			{
				if (!(tagg is NamedSelectionTagg))
				{
					continue;
				}
				NamedSelectionTagg namedSelectionTagg = tagg as NamedSelectionTagg;
				if (nsPoints != null && nsPoints.TryGetValue(namedSelectionTagg.Name, out var value))
				{
					foreach (PointWeight item in value)
					{
						byte b = (byte)(-item.weight);
						if (b != 0)
						{
							namedSelectionTagg.points[item.pointIndex] = b;
						}
					}
				}
				if (nsFaces == null || !nsFaces.TryGetValue(namedSelectionTagg.Name, out var value2))
				{
					continue;
				}
				foreach (int item2 in value2)
				{
					namedSelectionTagg.faces[item2] = 1;
				}
			}
		}

		private static void ConvertPoints(BisDll.Model.ODOL.ODOL odol, MLOD_LOD dstLod, LOD srcLod)
		{
			Vector3P boundingCenter = odol.modelInfo.boundingCenter;
			_ = odol.modelInfo.bboxMinVisual;
			_ = odol.modelInfo.bboxMaxVisual;
			int num = srcLod.Vertices.Length;
			dstLod.points = new Point[num];
			for (int i = 0; i < num; i++)
			{
				Vector3P pos = srcLod.Vertices[i] + boundingCenter;
				dstLod.points[i] = new Point(pos, clipFlagsToPointFlags(srcLod.ClipFlags[i]));
			}
		}

		private static void ConvertFaces(BisDll.Model.ODOL.ODOL odol, MLOD_LOD dstLod, LOD srcLOD)
		{
			List<Face> list = new List<Face>(srcLOD.VertexCount * 2);
			Section[] sections = srcLOD.Sections;
			foreach (Section section in sections)
			{
				float[] uVData = srcLOD.UVSets[0].UVData;
				uint[] faceIndexes = section.getFaceIndexes(srcLOD.Faces);
				foreach (uint num in faceIndexes)
				{
					int num2 = srcLOD.Faces[num].VertexIndices.Length;
					Vertex[] array = new Vertex[num2];
					for (int k = 0; k < num2; k++)
					{
						int num3 = srcLOD.Faces[num].VertexIndices[num2 - 1 - k];
						array[k] = new Vertex(num3, num3, uVData[num3 * 2], uVData[num3 * 2 + 1]);
					}
					string texture = ((section.textureIndex == -1) ? "" : srcLOD.Textures[section.textureIndex]);
					string material = ((section.materialIndex == -1) ? "" : srcLOD.Materials[section.materialIndex].materialName);
					Face item = new Face(num2, array, FaceFlags.DEFAULT, texture, material);
					list.Add(item);
				}
			}
			dstLod.faces = list.ToArray();
		}

		private static void ReconstructNamedSelectionBySections(LOD src, out Dictionary<string, List<PointWeight>> points, out Dictionary<string, List<int>> faces)
		{
			points = new Dictionary<string, List<PointWeight>>(src.NamedSelections.Length * 2);
			faces = new Dictionary<string, List<int>>(src.NamedSelections.Length * 2);
			NamedSelection[] namedSelections = src.NamedSelections;
			foreach (NamedSelection namedSelection in namedSelections)
			{
				if (namedSelection.IsSectional)
				{
					IEnumerable<uint> source = namedSelection.Sections.SelectMany((int si) => src.Sections[si].getFaceIndexes(src.Faces));
					IEnumerable<PointWeight> source2 = from vi in source.SelectMany((uint fi) => src.Faces[fi].VertexIndices)
						select new PointWeight(vi, byte.MaxValue);
					faces[namedSelection.Name] = source.Select((uint fi) => (int)fi).ToList();
					points[namedSelection.Name] = source2.ToList();
				}
			}
		}

		private static void ReconstructProxies(LOD src, out Dictionary<string, List<PointWeight>> points, out Dictionary<string, List<int>> faces)
		{
			points = new Dictionary<string, List<PointWeight>>(src.NamedSelections.Length * 2);
			faces = new Dictionary<string, List<int>>(src.NamedSelections.Length * 2);
			for (int i = 0; i < src.Faces.Length; i++)
			{
				Polygon polygon = src.Faces[i];
				if (polygon.VertexIndices.Length != 3)
				{
					continue;
				}
				VertexIndex vertexIndex = polygon.VertexIndices[0];
				VertexIndex vertexIndex2 = polygon.VertexIndices[1];
				VertexIndex vertexIndex3 = polygon.VertexIndices[2];
				Vector3P v = src.Vertices[(int)vertexIndex];
				Vector3P v2 = src.Vertices[(int)vertexIndex2];
				Vector3P v3 = src.Vertices[(int)vertexIndex3];
				float v4 = v.Distance(v2);
				float v5 = v.Distance(v3);
				float v6 = v2.Distance(v3);
				if (v4 > v5)
				{
					Methods.Swap(ref v2, ref v3);
					Methods.Swap(ref v4, ref v5);
				}
				if (v4 > v6)
				{
					Methods.Swap(ref v, ref v3);
					Methods.Swap(ref v4, ref v6);
				}
				if (v5 > v6)
				{
					Methods.Swap(ref v, ref v2);
					Methods.Swap(ref v5, ref v6);
				}
				Vector3P vector3P = v;
				Vector3P vector3P2 = v2 - v;
				Vector3P vector3P3 = v3 - v;
				vector3P2.Normalize();
				vector3P3.Normalize();
				if (!Methods.EqualsFloat(vector3P3 * vector3P2, 0f, 0.05f))
				{
					continue;
				}
				for (int j = 0; j < src.Proxies.Length; j++)
				{
					Vector3P position = src.Proxies[j].transformation.Position;
					Vector3P up = src.Proxies[j].transformation.Orientation.Up;
					Vector3P dir = src.Proxies[j].transformation.Orientation.Dir;
					if (vector3P.Equals(position) && vector3P2.Equals(dir) && vector3P3.Equals(up))
					{
						Proxy proxy = src.Proxies[j];
						string name = src.NamedSelections[proxy.namedSelectionIndex].Name;
						if (!faces.ContainsKey(name))
						{
							faces[name] = i.Yield().ToList();
							points[name] = Methods.Yield<PointWeight>(new PointWeight(vertexIndex, byte.MaxValue), new PointWeight(vertexIndex2, byte.MaxValue), new PointWeight(vertexIndex3, byte.MaxValue)).ToList();
							break;
						}
					}
				}
			}
		}

		private static void ReconstructNamedSelectionsByBones(LOD src, Skeleton skeleton, out Dictionary<string, List<PointWeight>> points)
		{
			points = new Dictionary<string, List<PointWeight>>(src.NamedSelections.Length * 2);
			if (src.VertexBoneRef.Length == 0)
			{
				return;
			}
			ushort num = 0;
			AnimationRTWeight[] vertexBoneRef = src.VertexBoneRef;
			for (int i = 0; i < vertexBoneRef.Length; i++)
			{
				AnimationRTPair[] animationRTPairs = vertexBoneRef[i].AnimationRTPairs;
				foreach (AnimationRTPair obj in animationRTPairs)
				{
					byte selectionIndex = obj.SelectionIndex;
					byte weight = obj.Weight;
					int num2 = src.SubSkeletonsToSkeleton[selectionIndex];
					string key = skeleton.bones[num2 * 2];
					PointWeight item = new PointWeight(num, weight);
					if (!points.TryGetValue(key, out var value))
					{
						value = new List<PointWeight>(10000);
						value.Add(item);
						points[key] = value;
					}
					else
					{
						value.Add(item);
					}
				}
				num++;
			}
		}

		private static IEnumerable<NamedSelectionTagg> createNamedSelectionTaggs(LOD src)
		{
			int nPoints = src.VertexCount;
			int nFaces = src.Faces.Length;
			NamedSelection[] namedSelections = src.NamedSelections;
			foreach (NamedSelection namedSelection in namedSelections)
			{
				NamedSelectionTagg namedSelectionTagg = new NamedSelectionTagg();
				namedSelectionTagg.Name = namedSelection.Name;
				namedSelectionTagg.DataSize = (uint)(nPoints + nFaces);
				namedSelectionTagg.points = new byte[nPoints];
				namedSelectionTagg.faces = new byte[nFaces];
				bool flag = namedSelection.SelectedVerticesWeights.Length != 0;
				int num = 0;
				VertexIndex[] selectedVertices = namedSelection.SelectedVertices;
				foreach (int num2 in selectedVertices)
				{
					byte b = (byte)((!flag) ? 1 : ((byte)(-namedSelection.SelectedVerticesWeights[num++])));
					namedSelectionTagg.points[num2] = b;
				}
				selectedVertices = namedSelection.SelectedFaces;
				foreach (int num3 in selectedVertices)
				{
					namedSelectionTagg.faces[num3] = 1;
				}
				yield return namedSelectionTagg;
			}
		}

		private static IEnumerable<AnimationTagg> createAnimTaggs(LOD src)
		{
			Keyframe[] frames = src.Frames;
			foreach (Keyframe keyframe in frames)
			{
				int num = keyframe.points.Length;
				AnimationTagg animationTagg = new AnimationTagg();
				animationTagg.Name = "#Animation#";
				animationTagg.DataSize = (uint)(num * 12 + 4);
				animationTagg.frameTime = keyframe.time;
				animationTagg.framePoints = new Vector3P[num];
				Array.Copy(keyframe.points, animationTagg.framePoints, num);
				yield return animationTagg;
			}
		}

		private static MassTagg createMassTagg(int nPoints, float totalMass)
		{
			MassTagg massTagg = new MassTagg();
			massTagg.Name = "#Mass#";
			massTagg.DataSize = (uint)(nPoints * 4);
			massTagg.mass = new float[nPoints];
			for (int i = 0; i < nPoints; i++)
			{
				massTagg.mass[i] = totalMass / (float)nPoints;
			}
			return massTagg;
		}

		private static IEnumerable<UVSetTagg> createUVSetTaggs(LOD src)
		{
			int nFaces = src.Faces.Length;
			int i = 0;
			while (i < src.UVSets.Length)
			{
				UVSetTagg uVSetTagg = new UVSetTagg();
				uVSetTagg.Name = "#UVSet#";
				uVSetTagg.uvSetNr = (uint)i;
				uVSetTagg.faceUVs = new float[nFaces][,];
				float[] uVData = src.UVSets[i].UVData;
				uint num = 4u;
				for (int j = 0; j < nFaces; j++)
				{
					Polygon polygon = src.Faces[j];
					int num2 = polygon.VertexIndices.Length;
					uVSetTagg.faceUVs[j] = new float[num2, 2];
					for (int k = 0; k < num2; k++)
					{
						VertexIndex vertexIndex = polygon.VertexIndices[num2 - 1 - k];
						uVSetTagg.faceUVs[j][k, 0] = uVData[(int)vertexIndex * 2];
						uVSetTagg.faceUVs[j][k, 1] = uVData[(int)vertexIndex * 2 + 1];
						num += 8;
					}
				}
				uVSetTagg.DataSize = num;
				yield return uVSetTagg;
				int num3 = i + 1;
				i = num3;
			}
		}

		private static IEnumerable<PropertyTagg> createPropertyTaggs(LOD src)
		{
			int i = 0;
			while (i < src.NamedProperties.Length / 2)
			{
				yield return new PropertyTagg
				{
					Name = "#Property#",
					DataSize = 128u,
					name = src.NamedProperties[i, 0],
					value = src.NamedProperties[i, 1]
				};
				int num = i + 1;
				i = num;
			}
		}
	}
	public static class P3D_FaceFlags
	{
		public static byte GetUserValue(this FaceFlags flags)
		{
			return (byte)((long)((ulong)flags & 0xFE000000uL) >> 24);
		}

		public static void SetUserValue(this FaceFlags flags, byte value)
		{
			flags &= (FaceFlags)33554431;
			flags += value << 24;
		}
	}
	[Flags]
	public enum FaceFlags
	{
		DEFAULT = 0,
		SHADOW_OFF = 0x10,
		MERGING_OFF = 0x1000000,
		ZBIAS_LOW = 0x100,
		ZBIAS_MID = 0x200,
		ZBIAS_HIGH = 0x300,
		LIGHTNING_BOTH = 0x20,
		LIGHTNING_POSITION = 0x80,
		LIGHTNING_FLAT = 0x200000,
		LIGHTNING_REVERSED = 0x100000
	}
	[Flags]
	public enum PointFlags : uint
	{
		NONE = 0u,
		ONLAND = 1u,
		UNDERLAND = 2u,
		ABOVELAND = 4u,
		KEEPLAND = 8u,
		LAND_MASK = 0xFu,
		DECAL = 0x100u,
		VDECAL = 0x200u,
		DECAL_MASK = 0x300u,
		NOLIGHT = 0x10u,
		AMBIENT = 0x20u,
		FULLLIGHT = 0x40u,
		HALFLIGHT = 0x80u,
		LIGHT_MASK = 0xF0u,
		NOFOG = 0x1000u,
		SKYFOG = 0x2000u,
		FOG_MASK = 0x3000u,
		USER_MASK = 0xFF0000u,
		USER_STEP = 0x10000u,
		SPECIAL_MASK = 0xF000000u,
		SPECIAL_HIDDEN = 0x1000000u,
		ALL_FLAGS = 0xFFF33FFu
	}
	public enum LodName
	{
		ViewGunner,
		ViewPilot,
		ViewCargo,
		Geometry,
		Memory,
		LandContact,
		Roadway,
		Paths,
		HitPoints,
		ViewGeometry,
		FireGeometry,
		ViewCargoGeometry,
		ViewCargoFireGeometry,
		ViewCommander,
		ViewCommanderGeometry,
		ViewCommanderFireGeometry,
		ViewPilotGeometry,
		ViewPilotFireGeometry,
		ViewGunnerGeometry,
		ViewGunnerFireGeometry,
		SubParts,
		ShadowVolumeViewCargo,
		ShadowVolumeViewPilot,
		ShadowVolumeViewGunner,
		Wreck,
		PhysX,
		ShadowVolume,
		Resolution,
		Undefined
	}
	public static class Resolution
	{
		private const float specialLod = 1E+15f;

		public const float GEOMETRY = 1E+13f;

		public const float BUOYANCY = 2E+13f;

		public const float PHYSXOLD = 3E+13f;

		public const float PHYSX = 4E+13f;

		public const float MEMORY = 1E+15f;

		public const float LANDCONTACT = 2E+15f;

		public const float ROADWAY = 3E+15f;

		public const float PATHS = 4E+15f;

		public const float HITPOINTS = 5E+15f;

		public const float VIEW_GEOMETRY = 6E+15f;

		public const float FIRE_GEOMETRY = 7E+15f;

		public const float VIEW_GEOMETRY_CARGO = 8E+15f;

		public const float VIEW_GEOMETRY_PILOT = 1.3E+16f;

		public const float VIEW_GEOMETRY_GUNNER = 1.5E+16f;

		public const float FIRE_GEOMETRY_GUNNER = 1.6E+16f;

		public const float SUBPARTS = 1.7E+16f;

		public const float SHADOWVOLUME_CARGO = 1.8E+16f;

		public const float SHADOWVOLUME_PILOT = 1.9E+16f;

		public const float SHADOWVOLUME_GUNNER = 2E+16f;

		public const float WRECK = 2.1E+16f;

		public const float VIEW_COMMANDER = 1E+16f;

		public const float VIEW_GUNNER = 1000f;

		public const float VIEW_PILOT = 1100f;

		public const float VIEW_CARGO = 1200f;

		public const float SHADOWVOLUME = 10000f;

		public const float SHADOWBUFFER = 11000f;

		public const float SHADOW_MIN = 10000f;

		public const float SHADOW_MAX = 20000f;

		public static bool KeepsNamedSelections(float r)
		{
			if (r != 1E+15f && r != 7E+15f && r != 1E+13f && r != 6E+15f && r != 1.3E+16f && r != 1.5E+16f && r != 8E+15f && r != 4E+15f && r != 5E+15f && r != 4E+13f)
			{
				return r == 2E+13f;
			}
			return true;
		}

		public static LodName getLODType(this float res)
		{
			if (res == 1E+15f)
			{
				return LodName.Memory;
			}
			if (res == 2E+15f)
			{
				return LodName.LandContact;
			}
			if (res == 3E+15f)
			{
				return LodName.Roadway;
			}
			if (res == 4E+15f)
			{
				return LodName.Paths;
			}
			if (res == 5E+15f)
			{
				return LodName.HitPoints;
			}
			if (res == 6E+15f)
			{
				return LodName.ViewGeometry;
			}
			if (res == 7E+15f)
			{
				return LodName.FireGeometry;
			}
			if (res == 8E+15f)
			{
				return LodName.ViewCargoGeometry;
			}
			if (res == 9E+15f)
			{
				return LodName.ViewCargoFireGeometry;
			}
			if (res == 1E+16f)
			{
				return LodName.ViewCommander;
			}
			if (res == 1.1E+16f)
			{
				return LodName.ViewCommanderGeometry;
			}
			if (res == 1.2E+16f)
			{
				return LodName.ViewCommanderFireGeometry;
			}
			if (res == 1.3E+16f)
			{
				return LodName.ViewPilotGeometry;
			}
			if (res == 1.4E+16f)
			{
				return LodName.ViewPilotFireGeometry;
			}
			if (res == 1.4999999E+16f)
			{
				return LodName.ViewGunnerGeometry;
			}
			if (res == 1.6E+16f)
			{
				return LodName.ViewGunnerFireGeometry;
			}
			if (res == 1.7E+16f)
			{
				return LodName.SubParts;
			}
			if (res == 1.8E+16f)
			{
				return LodName.ShadowVolumeViewCargo;
			}
			if (res == 1.9E+16f)
			{
				return LodName.ShadowVolumeViewPilot;
			}
			if (res == 2E+16f)
			{
				return LodName.ShadowVolumeViewGunner;
			}
			if (res == 2.1E+16f)
			{
				return LodName.Wreck;
			}
			if (res == 1000f)
			{
				return LodName.ViewGunner;
			}
			if (res == 1100f)
			{
				return LodName.ViewPilot;
			}
			if (res == 1200f)
			{
				return LodName.ViewCargo;
			}
			if (res == 1E+13f)
			{
				return LodName.Geometry;
			}
			if (res == 4E+13f)
			{
				return LodName.PhysX;
			}
			if ((double)res >= 10000.0 && (double)res <= 20000.0)
			{
				return LodName.ShadowVolume;
			}
			return LodName.Resolution;
		}

		public static string getLODName(this float res)
		{
			LodName lODType = res.getLODType();
			return lODType switch
			{
				LodName.Resolution => res.ToString("#.000"), 
				LodName.ShadowVolume => "ShadowVolume" + (res - 10000f).ToString("#.000"), 
				_ => Enum.GetName(typeof(LodName), lODType), 
			};
		}

		public static bool IsResolution(float r)
		{
			return r < 10000f;
		}

		public static bool IsShadow(float r)
		{
			if ((!(r >= 10000f) || !(r < 20000f)) && r != 2E+16f && r != 1.9E+16f)
			{
				return r == 1.8E+16f;
			}
			return true;
		}

		public static bool IsVisual(float r)
		{
			if (!IsResolution(r) && r != 1200f && r != 1000f && r != 1100f)
			{
				return r == 1E+16f;
			}
			return true;
		}
	}
	public abstract class P3D_LOD
	{
		protected float resolution;

		public string Name => resolution.getLODName();

		public float Resolution => resolution;

		public abstract Vector3P[] Points { get; }

		public abstract Vector3P[] Normals { get; }

		public abstract string[] Textures { get; }

		public abstract string[] MaterialNames { get; }
	}
	public abstract class P3D
	{
		public uint Version { get; protected set; }

		public abstract P3D_LOD[] LODs { get; }

		public abstract float Mass { get; }

		public static P3D GetInstance(string fileName)
		{
			return GetInstance(File.OpenRead(fileName));
		}

		public static P3D GetInstance(System.IO.Stream stream)
		{
			string text = new BinaryReaderEx(stream).ReadAscii(4);
			stream.Position -= 4L;
			if (text == "ODOL")
			{
				return new BisDll.Model.ODOL.ODOL(stream);
			}
			if (text == "MLOD")
			{
				return new BisDll.Model.MLOD.MLOD(stream);
			}
			throw new FormatException();
		}

		public virtual P3D_LOD getLOD(float resolution)
		{
			return LODs.FirstOrDefault((P3D_LOD lod) => lod.Resolution == resolution);
		}
	}
}
namespace BisDll.Model.ODOL
{
	public class Animations
	{
		public class AnimationClass : IDeserializable
		{
			public enum AnimType
			{
				Rotation,
				RotationX,
				RotationY,
				RotationZ,
				Translation,
				TranslationX,
				TranslationY,
				TranslationZ,
				Direct,
				Hide
			}

			public enum AnimAddress
			{
				AnimClamp,
				AnimLoop,
				AnimMirror,
				NAnimAddress
			}

			public AnimType animType;

			private string animName;

			private string animSource;

			private float minValue;

			private float maxValue;

			private float minPhase;

			private float maxPhase;

			private float animPeriod;

			private float initPhase;

			private AnimAddress sourceAddress;

			private float angle0;

			private float angle1;

			private float offset0;

			private float offset1;

			private Vector3P axisPos;

			private Vector3P axisDir;

			private float angle;

			private float axisOffset;

			private float hideValue;

			public void ReadObject(BinaryReaderEx input)
			{
				int version = input.Version;
				animType = (AnimType)input.ReadUInt32();
				animName = input.ReadAsciiz();
				animSource = input.ReadAsciiz();
				minPhase = input.ReadSingle();
				maxPhase = input.ReadSingle();
				minValue = input.ReadSingle();
				maxValue = input.ReadSingle();
				if (version >= 56)
				{
					animPeriod = input.ReadSingle();
					initPhase = input.ReadSingle();
				}
				sourceAddress = (AnimAddress)input.ReadUInt32();
				switch (animType)
				{
				case AnimType.Rotation:
				case AnimType.RotationX:
				case AnimType.RotationY:
				case AnimType.RotationZ:
					angle0 = input.ReadSingle();
					angle1 = input.ReadSingle();
					break;
				case AnimType.Translation:
				case AnimType.TranslationX:
				case AnimType.TranslationY:
				case AnimType.TranslationZ:
					offset0 = input.ReadSingle();
					offset1 = input.ReadSingle();
					break;
				case AnimType.Direct:
					axisPos = new Vector3P(input);
					axisDir = new Vector3P(input);
					angle = input.ReadSingle();
					axisOffset = input.ReadSingle();
					break;
				case AnimType.Hide:
					hideValue = input.ReadSingle();
					if (version >= 55)
					{
						input.ReadSingle();
					}
					break;
				default:
					throw new Exception("Unknown AnimType encountered: " + animType);
				}
			}
		}

		private AnimationClass[] animationClasses;

		private int nAnimLODs;

		private uint[][][] Bones2Anims;

		private int[][] Anims2Bones;

		private Vector3P[][][] axisData;

		public void read(BinaryReaderEx input)
		{
			animationClasses = input.ReadArray<AnimationClass>();
			int num = animationClasses.Length;
			nAnimLODs = input.ReadInt32();
			Bones2Anims = new uint[nAnimLODs][][];
			for (int i = 0; i < nAnimLODs; i++)
			{
				uint num2 = input.ReadUInt32();
				Bones2Anims[i] = new uint[num2][];
				for (int j = 0; j < num2; j++)
				{
					uint num3 = input.ReadUInt32();
					Bones2Anims[i][j] = new uint[num3];
					for (int k = 0; k < num3; k++)
					{
						Bones2Anims[i][j][k] = input.ReadUInt32();
					}
				}
			}
			Anims2Bones = new int[nAnimLODs][];
			axisData = new Vector3P[nAnimLODs][][];
			for (int l = 0; l < nAnimLODs; l++)
			{
				Anims2Bones[l] = new int[num];
				axisData[l] = new Vector3P[num][];
				for (int m = 0; m < num; m++)
				{
					Anims2Bones[l][m] = input.ReadInt32();
					if (Anims2Bones[l][m] != -1 && animationClasses[m].animType != AnimationClass.AnimType.Direct && animationClasses[m].animType != AnimationClass.AnimType.Hide)
					{
						axisData[l][m] = new Vector3P[2];
						axisData[l][m][0] = new Vector3P(input);
						axisData[l][m][1] = new Vector3P(input);
					}
				}
			}
		}
	}
	public class Keyframe : IDeserializable
	{
		public float time;

		public Vector3P[] points;

		public void ReadObject(BinaryReaderEx input)
		{
			time = input.ReadSingle();
			uint num = input.ReadUInt32();
			points = new Vector3P[num];
			for (int i = 0; i < num; i++)
			{
				points[i] = new Vector3P(input);
			}
		}
	}
	public class LOD : P3D_LOD, IComparable<LOD>
	{
		private struct PointWeight
		{
			public int pointIndex;

			public byte weight;

			public PointWeight(int index, byte weight)
			{
				pointIndex = index;
				this.weight = weight;
			}
		}

		private uint odolVersion;

		private Proxy[] proxies;

		private int[] subSkeletonsToSkeleton;

		private SubSkeletonIndexSet[] skeletonToSubSkeleton;

		private uint vertexCount;

		private float faceArea;

		private ClipFlags[] clipOldFormat;

		private ClipFlags[] clip;

		private ClipFlags orHints;

		private ClipFlags andHints;

		private Vector3P bMin;

		private Vector3P bMax;

		private Vector3P bCenter;

		private float bRadius;

		private string[] textures;

		private EmbeddedMaterial[] materials;

		private VertexIndex[] pointToVertex;

		private VertexIndex[] vertexToPoint;

		private Polygons polygons;

		private Section[] sections;

		private NamedSelection[] namedSelections;

		private uint nNamedProperties;

		private string[,] namedProperties;

		private Keyframe[] frames;

		private int colorTop;

		private int color;

		private int special;

		private bool vertexBoneRefIsSimple;

		private uint sizeOfRestData;

		private uint nUVSets;

		private UVSet[] uvSets;

		private Vector3P[] vertices;

		private Vector3P[] normals;

		private STPair[] STCoords;

		private AnimationRTWeight[] vertexBoneRef;

		private VertexNeighborInfo[] neighborBoneRef;

		public NamedSelection[] NamedSelections => namedSelections;

		public override string[] MaterialNames => materials.Select((EmbeddedMaterial m) => m.materialName).ToArray();

		public EmbeddedMaterial[] Materials => materials;

		public int VertexCount => vertices.Length;

		public int SectionCount => sections.Length;

		public int TextureCount => textures.Length;

		public int PolygonCount => polygons.Faces.Length;

		public int MaterialCount => materials.Length;

		public AnimationRTWeight[] VertexBoneRef => vertexBoneRef;

		public VertexNeighborInfo[] NeighborBoneRef => neighborBoneRef;

		public ClipFlags[] ClipFlags
		{
			get
			{
				if (odolVersion < 50)
				{
					return clipOldFormat;
				}
				return clip;
			}
		}

		public Vector3P[] Vertices => vertices;

		public override Vector3P[] Normals => normals;

		public Section[] Sections => sections;

		public UVSet[] UVSets => uvSets;

		public Polygon[] Faces => polygons.Faces;

		public string[,] NamedProperties => namedProperties;

		public Keyframe[] Frames => frames;

		public int[] SubSkeletonsToSkeleton => subSkeletonsToSkeleton;

		public Proxy[] Proxies => proxies;

		public override Vector3P[] Points => Vertices;

		public override string[] Textures => textures;

		public void read(BinaryReaderEx input, float resolution)
		{
			odolVersion = (uint)input.Version;
			base.resolution = resolution;
			proxies = input.ReadArray<Proxy>();
			subSkeletonsToSkeleton = input.ReadIntArray();
			skeletonToSubSkeleton = input.ReadArray<SubSkeletonIndexSet>();
			if (odolVersion >= 50)
			{
				vertexCount = input.ReadUInt32();
			}
			else
			{
				int[] array = input.ReadCondensedIntArray();
				clipOldFormat = Array.ConvertAll(array, (int item) => (ClipFlags)item);
			}
			if (odolVersion >= 51)
			{
				faceArea = input.ReadSingle();
			}
			orHints = (ClipFlags)input.ReadInt32();
			andHints = (ClipFlags)input.ReadInt32();
			bMin = new Vector3P(input);
			bMax = new Vector3P(input);
			bCenter = new Vector3P(input);
			bRadius = input.ReadSingle();
			textures = input.ReadStringArray();
			materials = input.ReadArray<EmbeddedMaterial>();
			pointToVertex = input.ReadCompressedVertexIndexArray();
			vertexToPoint = input.ReadCompressedVertexIndexArray();
			polygons = new Polygons(input);
			sections = input.ReadArray<Section>();
			namedSelections = input.ReadArray<NamedSelection>();
			nNamedProperties = input.ReadUInt32();
			namedProperties = new string[nNamedProperties, 2];
			for (int num = 0; num < nNamedProperties; num++)
			{
				namedProperties[num, 0] = input.ReadAsciiz();
				namedProperties[num, 1] = input.ReadAsciiz();
			}
			frames = input.ReadArray<Keyframe>();
			colorTop = input.ReadInt32();
			color = input.ReadInt32();
			special = input.ReadInt32();
			vertexBoneRefIsSimple = input.ReadBoolean();
			sizeOfRestData = input.ReadUInt32();
			if (odolVersion >= 50)
			{
				int[] array2 = input.ReadCondensedIntArray();
				clip = Array.ConvertAll(array2, (int item) => (ClipFlags)item);
			}
			UVSet uVSet = new UVSet();
			uVSet.read(input, odolVersion);
			nUVSets = input.ReadUInt32();
			uvSets = new UVSet[nUVSets];
			uvSets[0] = uVSet;
			for (int num2 = 1; num2 < nUVSets; num2++)
			{
				uvSets[num2] = new UVSet();
				uvSets[num2].read(input, odolVersion);
			}
			vertices = input.ReadCompressedObjectArray<Vector3P>(12);
			if (odolVersion >= 45)
			{
				Vector3PCompressed[] array3 = input.ReadCondensedObjectArray<Vector3PCompressed>(4);
				normals = Array.ConvertAll(array3, (Converter<Vector3PCompressed, Vector3P>)((Vector3PCompressed item) => item));
			}
			else
			{
				normals = input.ReadCondensedObjectArray<Vector3P>(12);
			}
			STCoords = (STPair[])((odolVersion >= 45) ? ((Array)input.ReadCompressedObjectArray<STPairCompressed>(8)) : ((Array)input.ReadCompressedObjectArray<STPairUncompressed>(24)));
			vertexBoneRef = input.ReadCompressedObjectArray<AnimationRTWeight>(12);
			neighborBoneRef = input.ReadCompressedObjectArray<VertexNeighborInfo>(32);
			if (odolVersion >= 67)
			{
				input.ReadUInt32();
			}
			if (odolVersion >= 68)
			{
				input.ReadByte();
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			throw new NotImplementedException();
		}

		public int CompareTo(LOD other)
		{
			return resolution.CompareTo(other.resolution);
		}
	}
	public class EmbeddedMaterial : IDeserializable
	{
		private enum EFogMode
		{
			FM_None,
			FM_Fog,
			FM_Alpha,
			FM_FogAlpha
		}

		private enum EMainLight
		{
			ML_None,
			ML_Sun,
			ML_Sky,
			ML_Horizon,
			ML_Stars,
			ML_SunObject,
			ML_SunHaloObject,
			ML_MoonObject,
			ML_MoonHaloObject
		}

		public enum PixelShaderID : uint
		{
			PSNormal = 0u,
			PSNormalDXTA = 1u,
			PSNormalMap = 2u,
			PSNormalMapThrough = 3u,
			PSNormalMapGrass = 4u,
			PSNormalMapDiffuse = 5u,
			PSDetail = 6u,
			PSInterpolation = 7u,
			PSWater = 8u,
			PSWaterSimple = 9u,
			PSWhite = 10u,
			PSWhiteAlpha = 11u,
			PSAlphaShadow = 12u,
			PSAlphaNoShadow = 13u,
			PSDummy0 = 14u,
			PSDetailMacroAS = 15u,
			PSNormalMapMacroAS = 16u,
			PSNormalMapDiffuseMacroAS = 17u,
			PSNormalMapSpecularMap = 18u,
			PSNormalMapDetailSpecularMap = 19u,
			PSNormalMapMacroASSpecularMap = 20u,
			PSNormalMapDetailMacroASSpecularMap = 21u,
			PSNormalMapSpecularDIMap = 22u,
			PSNormalMapDetailSpecularDIMap = 23u,
			PSNormalMapMacroASSpecularDIMap = 24u,
			PSNormalMapDetailMacroASSpecularDIMap = 25u,
			PSTerrain1 = 26u,
			PSTerrain2 = 27u,
			PSTerrain3 = 28u,
			PSTerrain4 = 29u,
			PSTerrain5 = 30u,
			PSTerrain6 = 31u,
			PSTerrain7 = 32u,
			PSTerrain8 = 33u,
			PSTerrain9 = 34u,
			PSTerrain10 = 35u,
			PSTerrain11 = 36u,
			PSTerrain12 = 37u,
			PSTerrain13 = 38u,
			PSTerrain14 = 39u,
			PSTerrain15 = 40u,
			PSTerrainSimple1 = 41u,
			PSTerrainSimple2 = 42u,
			PSTerrainSimple3 = 43u,
			PSTerrainSimple4 = 44u,
			PSTerrainSimple5 = 45u,
			PSTerrainSimple6 = 46u,
			PSTerrainSimple7 = 47u,
			PSTerrainSimple8 = 48u,
			PSTerrainSimple9 = 49u,
			PSTerrainSimple10 = 50u,
			PSTerrainSimple11 = 51u,
			PSTerrainSimple12 = 52u,
			PSTerrainSimple13 = 53u,
			PSTerrainSimple14 = 54u,
			PSTerrainSimple15 = 55u,
			PSGlass = 56u,
			PSNonTL = 57u,
			PSNormalMapSpecularThrough = 58u,
			PSGrass = 59u,
			PSNormalMapThroughSimple = 60u,
			PSNormalMapSpecularThroughSimple = 61u,
			PSRoad = 62u,
			PSShore = 63u,
			PSShoreWet = 64u,
			PSRoad2Pass = 65u,
			PSShoreFoam = 66u,
			PSNonTLFlare = 67u,
			PSNormalMapThroughLowEnd = 68u,
			PSTerrainGrass1 = 69u,
			PSTerrainGrass2 = 70u,
			PSTerrainGrass3 = 71u,
			PSTerrainGrass4 = 72u,
			PSTerrainGrass5 = 73u,
			PSTerrainGrass6 = 74u,
			PSTerrainGrass7 = 75u,
			PSTerrainGrass8 = 76u,
			PSTerrainGrass9 = 77u,
			PSTerrainGrass10 = 78u,
			PSTerrainGrass11 = 79u,
			PSTerrainGrass12 = 80u,
			PSTerrainGrass13 = 81u,
			PSTerrainGrass14 = 82u,
			PSTerrainGrass15 = 83u,
			PSCrater1 = 84u,
			PSCrater2 = 85u,
			PSCrater3 = 86u,
			PSCrater4 = 87u,
			PSCrater5 = 88u,
			PSCrater6 = 89u,
			PSCrater7 = 90u,
			PSCrater8 = 91u,
			PSCrater9 = 92u,
			PSCrater10 = 93u,
			PSCrater11 = 94u,
			PSCrater12 = 95u,
			PSCrater13 = 96u,
			PSCrater14 = 97u,
			PSSprite = 98u,
			PSSpriteSimple = 99u,
			PSCloud = 100u,
			PSHorizon = 101u,
			PSSuper = 102u,
			PSMulti = 103u,
			PSTerrainX = 104u,
			PSTerrainSimpleX = 105u,
			PSTerrainGrassX = 106u,
			PSTree = 107u,
			PSTreePRT = 108u,
			PSTreeSimple = 109u,
			PSSkin = 110u,
			PSCalmWater = 111u,
			PSTreeAToC = 112u,
			PSGrassAToC = 113u,
			PSTreeAdv = 114u,
			PSTreeAdvSimple = 115u,
			PSTreeAdvTrunk = 116u,
			PSTreeAdvTrunkSimple = 117u,
			PSTreeAdvAToC = 118u,
			PSTreeAdvSimpleAToC = 119u,
			PSTreeSN = 120u,
			PSSpriteExtTi = 121u,
			PSTerrainSNX = 122u,
			PSSimulWeatherClouds = 123u,
			PSSimulWeatherCloudsWithLightning = 124u,
			PSSimulWeatherCloudsCPU = 125u,
			PSSimulWeatherCloudsWithLightningCPU = 126u,
			PSSuperExt = 127u,
			PSSuperAToC = 128u,
			NPixelShaderID = 129u,
			PSNone = 129u,
			PSUninitialized = uint.MaxValue
		}

		public enum VertexShaderID
		{
			VSBasic,
			VSNormalMap,
			VSNormalMapDiffuse,
			VSGrass,
			VSDummy1,
			VSDummy2,
			VSShadowVolume,
			VSWater,
			VSWaterSimple,
			VSSprite,
			VSPoint,
			VSNormalMapThrough,
			VSDummy3,
			VSTerrain,
			VSBasicAS,
			VSNormalMapAS,
			VSNormalMapDiffuseAS,
			VSGlass,
			VSNormalMapSpecularThrough,
			VSNormalMapThroughNoFade,
			VSNormalMapSpecularThroughNoFade,
			VSShore,
			VSTerrainGrass,
			VSSuper,
			VSMulti,
			VSTree,
			VSTreeNoFade,
			VSTreePRT,
			VSTreePRTNoFade,
			VSSkin,
			VSCalmWater,
			VSTreeAdv,
			VSTreeAdvTrunk,
			VSSimulWeatherClouds,
			VSSimulWeatherCloudsCPU,
			NVertexShaderID
		}

		public string materialName;

		private uint version;

		private ColorP emissive;

		private ColorP ambient;

		private ColorP diffuse;

		private ColorP forcedDiffuse;

		private ColorP specular;

		private ColorP specularCopy;

		public float specularPower;

		public PixelShaderID pixelShader;

		public VertexShaderID vertexShader;

		private EMainLight mainLight;

		private EFogMode fogMode;

		public string surfaceFile;

		private uint nRenderFlags;

		private uint renderFlags;

		private uint nStages;

		private uint nTexGens;

		public StageTexture[] stageTextures;

		public StageTransform[] stageTransforms;

		private StageTexture stageTI = new StageTexture();

		public void writeToFile(string fileName)
		{
			List<string> list = new List<string>();
			string item = string.Concat("Emissive[] = ", emissive, ";");
			string item2 = string.Concat("Ambient[] = ", ambient, ";");
			string item3 = string.Concat("Diffuse[] = ", diffuse, ";");
			string item4 = string.Concat("forcedDiffuse[] = ", forcedDiffuse, ";");
			string item5 = string.Concat("Specular[] = ", specular, ";");
			string item6 = "specularPower = " + specularPower.ToString(new CultureInfo("en-GB").NumberFormat) + ";";
			string text = Enum.GetName(pixelShader.GetType(), pixelShader);
			string text2 = Enum.GetName(vertexShader.GetType(), vertexShader);
			if (text == "")
			{
				text = string.Concat("Unknown PixelShaderID (", pixelShader, ")");
			}
			if (text2 == "")
			{
				text2 = string.Concat("Unknown VertexShaderID (", vertexShader, ")");
			}
			string item7 = "PixelShader = " + text + ";";
			string item8 = "VertexShader = " + text2 + ";";
			list.Add(item);
			list.Add(item2);
			list.Add(item3);
			list.Add(item4);
			list.Add(item5);
			list.Add(item6);
			list.Add(item7);
			list.Add(item8);
			if (surfaceFile != "")
			{
				list.Add("surfaceInfo = " + surfaceFile + ";");
			}
			if (stageTextures != null)
			{
				for (int i = 0; i < stageTextures.Length; i++)
				{
					list.Add("class Stage" + i + 1);
					list.Add("{");
					list.Add("\tfilter = " + Enum.GetName(stageTextures[i].textureFilter.GetType(), stageTextures[i].textureFilter) + ";");
					list.Add("\ttexture = " + stageTextures[i].texture + ";");
					list.Add(string.Concat("\tuvSource = ", stageTransforms[i].uvSource, ";"));
					list.Add("\tclass uvTransform");
					list.Add("\t{");
					list.Add(string.Concat("\t\taside[] = ", stageTransforms[i].transformation.Orientation.Aside, ";"));
					list.Add(string.Concat("\t\tup[] = ", stageTransforms[i].transformation.Orientation.Up, ";"));
					list.Add(string.Concat("\t\tdir[] = ", stageTransforms[i].transformation.Orientation.Dir, ";"));
					list.Add(string.Concat("\t\tpos[] = ", stageTransforms[i].transformation.Position, ";"));
					list.Add("\t};");
					list.Add("};");
				}
			}
			File.WriteAllLines(fileName, list.ToArray());
		}

		public void ReadObject(BinaryReaderEx input)
		{
			materialName = input.ReadAsciiz();
			version = input.ReadUInt32();
			emissive.read(input);
			ambient.read(input);
			diffuse.read(input);
			forcedDiffuse.read(input);
			specular.read(input);
			specularCopy.read(input);
			specularPower = input.ReadSingle();
			pixelShader = (PixelShaderID)input.ReadUInt32();
			vertexShader = (VertexShaderID)input.ReadUInt32();
			mainLight = (EMainLight)input.ReadUInt32();
			fogMode = (EFogMode)input.ReadUInt32();
			if (version == 3)
			{
				input.ReadBoolean();
			}
			if (version >= 6)
			{
				surfaceFile = input.ReadAsciiz();
			}
			if (version >= 4)
			{
				nRenderFlags = input.ReadUInt32();
				renderFlags = input.ReadUInt32();
			}
			if (version > 6)
			{
				nStages = input.ReadUInt32();
			}
			if (version > 8)
			{
				nTexGens = input.ReadUInt32();
			}
			stageTextures = new StageTexture[nStages];
			stageTransforms = new StageTransform[nTexGens];
			if (version < 8)
			{
				for (int i = 0; i < nStages; i++)
				{
					stageTransforms[i] = new StageTransform(input);
					stageTextures[i].read(input, version);
				}
			}
			else
			{
				for (int j = 0; j < nStages; j++)
				{
					stageTextures[j] = new StageTexture();
					stageTextures[j].read(input, version);
				}
				for (int k = 0; k < nTexGens; k++)
				{
					stageTransforms[k] = new StageTransform(input);
				}
			}
			if (version >= 10)
			{
				stageTI.read(input, version);
			}
		}
	}
	public class StageTransform
	{
		public enum UVSource
		{
			UVNone,
			UVTex,
			UVTexWaterAnim,
			UVPos,
			UVNorm,
			UVTex1,
			UVWorldPos,
			UVWorldNorm,
			UVTexShoreAnim,
			NUVSource
		}

		public UVSource uvSource;

		public Matrix4P transformation;

		public StageTransform(BinaryReaderEx input)
		{
			uvSource = (UVSource)input.ReadUInt32();
			transformation = new Matrix4P(input);
		}
	}
	public class StageTexture
	{
		public enum TextureFilterType
		{
			Point,
			Linear,
			Triliniear,
			Anisotropic
		}

		public TextureFilterType textureFilter;

		public string texture;

		public uint stageID;

		public bool useWorldEnvMap;

		public void read(BinaryReaderEx input, uint matVersion)
		{
			if (matVersion >= 5)
			{
				textureFilter = (TextureFilterType)input.ReadUInt32();
			}
			texture = input.ReadAsciiz();
			if (matVersion >= 8)
			{
				stageID = input.ReadUInt32();
			}
			if (matVersion >= 11)
			{
				useWorldEnvMap = input.ReadBoolean();
			}
		}
	}
	public enum MapType
	{
		MapTree,
		MapSmallTree,
		MapBush,
		MapBuilding,
		MapHouse,
		MapForestBorder,
		MapForestTriangle,
		MapForestSquare,
		MapChurch,
		MapChapel,
		MapCross,
		MapRock,
		MapBunker,
		MapFortress,
		MapFountain,
		MapViewTower,
		MapLighthouse,
		MapQuay,
		MapFuelstation,
		MapHospital,
		MapFence,
		MapWall,
		MapHide,
		MapBusStop,
		MapRoad,
		MapForest,
		MapTransmitter,
		MapStack,
		MapRuin,
		MapTourism,
		MapWatertower,
		MapTrack,
		MapMainRoad,
		MapRocks,
		MapPowerLines,
		MapRailWay,
		NMapTypes
	}
	public class ODOL_ModelInfo
	{
		public int special { get; private set; }

		public float BoundingSphere { get; private set; }

		public float GeometrySphere { get; private set; }

		public int remarks { get; private set; }

		public int andHints { get; private set; }

		public int orHints { get; private set; }

		public Vector3P AimingCenter { get; private set; }

		public PackedColor color { get; private set; }

		public PackedColor colorType { get; private set; }

		public float viewDensity { get; private set; }

		public Vector3P bboxMin { get; private set; }

		public Vector3P bboxMax { get; private set; }

		public float propertyLodDensityCoef { get; private set; }

		public float propertyDrawImportance { get; private set; }

		public Vector3P bboxMinVisual { get; private set; }

		public Vector3P bboxMaxVisual { get; private set; }

		public Vector3P boundingCenter { get; private set; }

		public Vector3P geometryCenter { get; private set; }

		public Vector3P centerOfMass { get; private set; }

		public Matrix3P invInertia { get; private set; }

		public bool autoCenter { get; private set; }

		public bool lockAutoCenter { get; private set; }

		public bool canOcclude { get; private set; }

		public bool canBeOccluded { get; private set; }

		public bool AICovers { get; private set; }

		public float htMin { get; private set; }

		public float htMax { get; private set; }

		public float afMax { get; private set; }

		public float mfMax { get; private set; }

		public float mFact { get; private set; }

		public float tBody { get; private set; }

		public bool forceNotAlphaModel { get; private set; }

		public SBSource sbSource { get; private set; }

		public bool prefershadowvolume { get; private set; }

		public float shadowOffset { get; private set; }

		public bool animated { get; private set; }

		public Skeleton skeleton { get; private set; }

		public MapType mapType { get; private set; }

		public float[] massArray { get; private set; }

		public float mass { get; private set; }

		public float invMass { get; private set; }

		public float armor { get; private set; }

		public float invArmor { get; private set; }

		public float propertyExplosionShielding { get; private set; }

		public byte memory { get; private set; }

		public byte geometry { get; private set; }

		public byte geometrySimple { get; private set; }

		public byte geometryPhys { get; private set; }

		public byte geometryFire { get; private set; }

		public byte geometryView { get; private set; }

		public byte geometryViewPilot { get; private set; }

		public byte geometryViewGunner { get; private set; }

		public byte geometryViewCargo { get; private set; }

		public byte landContact { get; private set; }

		public byte roadway { get; private set; }

		public byte paths { get; private set; }

		public byte hitpoints { get; private set; }

		public byte minShadow { get; private set; }

		public bool canBlend { get; private set; }

		public string propertyClass { get; private set; }

		public string propertyDamage { get; private set; }

		public bool propertyFrequent { get; private set; }

		public int[] preferredShadowVolumeLod { get; private set; }

		public int[] preferredShadowBufferLod { get; private set; }

		public int[] preferredShadowBufferLodVis { get; private set; }

		internal ODOL_ModelInfo(BinaryReaderEx input, int nLods)
		{
			read(input, nLods);
		}

		public void read(BinaryReaderEx input, int nLods)
		{
			int version = input.Version;
			special = input.ReadInt32();
			BoundingSphere = input.ReadSingle();
			GeometrySphere = input.ReadSingle();
			remarks = input.ReadInt32();
			andHints = input.ReadInt32();
			orHints = input.ReadInt32();
			AimingCenter = new Vector3P(input);
			color = new PackedColor(input.ReadUInt32());
			colorType = new PackedColor(input.ReadUInt32());
			viewDensity = input.ReadSingle();
			bboxMin = new Vector3P(input);
			bboxMax = new Vector3P(input);
			if (version >= 70)
			{
				propertyLodDensityCoef = input.ReadSingle();
			}
			if (version >= 71)
			{
				propertyDrawImportance = input.ReadSingle();
			}
			if (version >= 52)
			{
				bboxMinVisual = new Vector3P(input);
				bboxMaxVisual = new Vector3P(input);
			}
			boundingCenter = new Vector3P(input);
			geometryCenter = new Vector3P(input);
			centerOfMass = new Vector3P(input);
			invInertia = new Matrix3P(input);
			autoCenter = input.ReadBoolean();
			lockAutoCenter = input.ReadBoolean();
			canOcclude = input.ReadBoolean();
			canBeOccluded = input.ReadBoolean();
			if (version >= 73)
			{
				AICovers = input.ReadBoolean();
			}
			if ((version >= 42 && version < 10000) || version >= 10042)
			{
				htMin = input.ReadSingle();
				htMax = input.ReadSingle();
				afMax = input.ReadSingle();
				mfMax = input.ReadSingle();
			}
			if ((version >= 43 && version < 10000) || version >= 10043)
			{
				mFact = input.ReadSingle();
				tBody = input.ReadSingle();
			}
			if (version >= 33)
			{
				forceNotAlphaModel = input.ReadBoolean();
			}
			if (version >= 37)
			{
				sbSource = (SBSource)input.ReadInt32();
				prefershadowvolume = input.ReadBoolean();
			}
			if (version >= 48)
			{
				shadowOffset = input.ReadSingle();
			}
			animated = input.ReadBoolean();
			skeleton = new Skeleton(input);
			mapType = (MapType)input.ReadByte();
			massArray = input.ReadCompressedFloatArray();
			mass = input.ReadSingle();
			invMass = input.ReadSingle();
			armor = input.ReadSingle();
			invArmor = input.ReadSingle();
			if (version >= 72)
			{
				propertyExplosionShielding = input.ReadSingle();
			}
			if (version >= 53)
			{
				geometrySimple = input.ReadByte();
			}
			if (version >= 54)
			{
				geometryPhys = input.ReadByte();
			}
			memory = input.ReadByte();
			geometry = input.ReadByte();
			geometryFire = input.ReadByte();
			geometryView = input.ReadByte();
			geometryViewPilot = input.ReadByte();
			geometryViewGunner = input.ReadByte();
			input.ReadSByte();
			geometryViewCargo = input.ReadByte();
			landContact = input.ReadByte();
			roadway = input.ReadByte();
			paths = input.ReadByte();
			hitpoints = input.ReadByte();
			minShadow = (byte)input.ReadUInt32();
			if (version >= 38)
			{
				canBlend = input.ReadBoolean();
			}
			propertyClass = input.ReadAsciiz();
			propertyDamage = input.ReadAsciiz();
			propertyFrequent = input.ReadBoolean();
			if (version >= 31)
			{
				input.ReadUInt32();
			}
			if (version >= 57)
			{
				preferredShadowVolumeLod = new int[nLods];
				preferredShadowBufferLod = new int[nLods];
				preferredShadowBufferLodVis = new int[nLods];
				for (int i = 0; i < nLods; i++)
				{
					preferredShadowVolumeLod[i] = input.ReadInt32();
				}
				for (int j = 0; j < nLods; j++)
				{
					preferredShadowBufferLod[j] = input.ReadInt32();
				}
				for (int k = 0; k < nLods; k++)
				{
					preferredShadowBufferLodVis[k] = input.ReadInt32();
				}
			}
		}
	}
	public class NamedSelection : IDeserializable
	{
		public string Name { get; private set; }

		public bool IsSectional { get; private set; }

		public VertexIndex[] SelectedFaces { get; private set; }

		public int[] Sections { get; private set; }

		public byte[] SelectedVerticesWeights { get; private set; }

		public VertexIndex[] SelectedVertices { get; private set; }

		public void ReadObject(BinaryReaderEx input)
		{
			Name = input.ReadAsciiz();
			SelectedFaces = input.ReadCompressedVertexIndexArray();
			input.ReadInt32();
			IsSectional = input.ReadBoolean();
			Sections = input.ReadCompressedIntArray();
			SelectedVertices = input.ReadCompressedVertexIndexArray();
			int expectedSize = input.ReadInt32();
			SelectedVerticesWeights = input.ReadCompressed((uint)expectedSize);
		}
	}
	public class ODOL : P3D
	{
		public const int LATEST_VERSION = 75;

		public const int MINIMAL_VERSION = 28;

		private string muzzleFlash;

		private uint appID;

		private int nLods;

		private float[] resolutions;

		public ODOL_ModelInfo modelInfo;

		private bool hasAnims;

		private Animations animations = new Animations();

		private uint[] lodStartAdresses;

		private uint[] lodEndAdresses;

		private bool[] permanent;

		private List<LoadableLodInfo> LoadableLodInfos;

		private LOD[] lods;

		public Skeleton Skeleton => modelInfo.skeleton;

		public override float Mass => modelInfo.mass;

		public override P3D_LOD[] LODs => lods;

		public ODOL(string fileName)
			: this(File.OpenRead(fileName))
		{
		}

		public ODOL(System.IO.Stream stream)
		{
			read(new BinaryReaderEx(stream));
		}

		public bool isSnappable()
		{
			LOD lOD = lods.FirstOrDefault((LOD l) => l.Resolution.getLODType() == LodName.Memory);
			if (lOD != null && lOD.NamedSelections.Where((NamedSelection ns) => ns.Name.Equals("lb", StringComparison.InvariantCultureIgnoreCase) || ns.Name.Equals("le", StringComparison.InvariantCultureIgnoreCase) || ns.Name.Equals("pb", StringComparison.InvariantCultureIgnoreCase) || ns.Name.Equals("pe", StringComparison.InvariantCultureIgnoreCase)).Count() >= 4)
			{
				return true;
			}
			return false;
		}

		private void read(BinaryReaderEx input)
		{
			string text = input.ReadAscii(4);
			if ("ODOL" != text)
			{
				throw new FormatException("ODOL signature is missing");
			}
			base.Version = input.ReadUInt32();
			if (base.Version > 75)
			{
				throw new FormatException("Unknown ODOL version");
			}
			if (base.Version < 28)
			{
				throw new FormatException("Old ODOL version is currently not supported");
			}
			input.Version = (int)base.Version;
			if (base.Version >= 44)
			{
				input.UseLZOCompression = true;
			}
			if (base.Version >= 64)
			{
				input.UseCompressionFlag = true;
			}
			if (base.Version >= 59)
			{
				appID = input.ReadUInt32();
			}
			if (base.Version >= 58)
			{
				muzzleFlash = input.ReadAsciiz();
			}
			if (base.Version >= 74)
			{
				input.ReadUInt32(); // v74+ unknown field 1
				input.ReadUInt32(); // v74+ unknown field 2
			}
			nLods = input.ReadInt32();
			resolutions = new float[nLods];
			for (int i = 0; i < nLods; i++)
			{
				resolutions[i] = input.ReadSingle();
			}
			modelInfo = new ODOL_ModelInfo(input, nLods);
			if (base.Version >= 30)
			{
				hasAnims = input.ReadBoolean();
				if (hasAnims)
				{
					animations.read(input);
				}
			}
			lodStartAdresses = new uint[nLods];
			lodEndAdresses = new uint[nLods];
			permanent = new bool[nLods];
			for (int j = 0; j < nLods; j++)
			{
				lodStartAdresses[j] = input.ReadUInt32();
			}
			for (int k = 0; k < nLods; k++)
			{
				lodEndAdresses[k] = input.ReadUInt32();
			}
			for (int l = 0; l < nLods; l++)
			{
				permanent[l] = input.ReadBoolean();
			}
			LoadableLodInfos = new List<LoadableLodInfo>(nLods);
			lods = new LOD[nLods];
			long position = input.Position;
			for (int m = 0; m < nLods; m++)
			{
				if (!permanent[m])
				{
					LoadableLodInfo loadableLodInfo = new LoadableLodInfo();
					loadableLodInfo.ReadObject(input);
					LoadableLodInfos.Add(loadableLodInfo);
					position = input.Position;
				}
				input.Position = lodStartAdresses[m];
				lods[m] = new LOD();
				lods[m].read(input, resolutions[m]);
				input.Position = position;
			}
			input.Position = lodEndAdresses.Max();
			input.Close();
		}

		public string[] getModelCfg()
		{
			throw new NotImplementedException();
		}
	}
	public abstract class VerySmallArray : IDeserializable
	{
		protected int nSmall;

		protected byte[] smallSpace;

		public void ReadObject(BinaryReaderEx input)
		{
			nSmall = input.ReadInt32();
			smallSpace = input.ReadBytes(8);
		}
	}
	public class AnimationRTWeight : VerySmallArray
	{
		public AnimationRTPair[] AnimationRTPairs
		{
			get
			{
				AnimationRTPair[] array = new AnimationRTPair[nSmall];
				for (int i = 0; i < nSmall; i++)
				{
					array[i] = new AnimationRTPair(smallSpace[i * 2], smallSpace[i * 2 + 1]);
				}
				return array;
			}
		}
	}
	public class AnimationRTPair
	{
		public byte SelectionIndex { get; }

		public byte Weight { get; }

		public AnimationRTPair(byte sel, byte weight)
		{
			SelectionIndex = sel;
			Weight = weight;
		}
	}
	public class VertexNeighborInfo : IDeserializable
	{
		public ushort PosA { get; private set; }

		public AnimationRTWeight RtwA { get; private set; }

		public ushort PosB { get; private set; }

		public AnimationRTWeight RtwB { get; private set; }

		public void ReadObject(BinaryReaderEx input)
		{
			PosA = input.ReadUInt16();
			input.ReadBytes(2);
			RtwA = new AnimationRTWeight();
			RtwA.ReadObject(input);
			PosB = input.ReadUInt16();
			input.ReadBytes(2);
			RtwB = new AnimationRTWeight();
			RtwB.ReadObject(input);
		}
	}
	public class LoadableLodInfo : IDeserializable
	{
		private int nFaces;

		private uint color;

		private int special;

		private uint orHints;

		private bool hasSkeleton;

		private int nVertices;

		private float faceArea;

		public void ReadObject(BinaryReaderEx input)
		{
			nFaces = input.ReadInt32();
			color = input.ReadUInt32();
			special = input.ReadInt32();
			orHints = input.ReadUInt32();
			int version = input.Version;
			if (version >= 39)
			{
				hasSkeleton = input.ReadBoolean();
			}
			if (version >= 51)
			{
				nVertices = input.ReadInt32();
				faceArea = input.ReadSingle();
			}
		}
	}
	public enum SBSource
	{
		SBS_Visual,
		SBS_ShadowVolume,
		SBS_Explicit,
		SBS_None,
		SBS_VisualEx
	}
	internal enum EAnimationType
	{
		AnimTypeNone,
		AnimTypeSoftware,
		AnimTypeHardware
	}
	public enum ClipFlags
	{
		ClipNone = 0,
		ClipFront = 1,
		ClipBack = 2,
		ClipLeft = 4,
		ClipRight = 8,
		ClipBottom = 16,
		ClipTop = 32,
		ClipUser0 = 64,
		ClipAll = 63,
		ClipLandMask = 3840,
		ClipLandStep = 256,
		ClipLandNone = 0,
		ClipLandOn = 256,
		ClipLandUnder = 512,
		ClipLandAbove = 1024,
		ClipLandKeep = 2048,
		ClipDecalMask = 12288,
		ClipDecalStep = 4096,
		ClipDecalNone = 0,
		ClipDecalNormal = 4096,
		ClipDecalVertical = 8192,
		ClipFogMask = 49152,
		ClipFogStep = 16384,
		ClipFogNormal = 0,
		ClipFogDisable = 16384,
		ClipFogSky = 32768,
		ClipLightMask = 983040,
		ClipLightStep = 65536,
		ClipLightNormal = 0,
		ClipLightLine = 524288,
		ClipUserMask = 267386880,
		ClipUserStep = 1048576,
		MaxUserValue = 255,
		ClipHints = 268435200
	}
	public class Polygons
	{
		public Polygon[] Faces { get; private set; }

		public Polygons(BinaryReaderEx input)
		{
			uint num = input.ReadUInt32();
			input.ReadUInt32();
			input.ReadUInt16();
			Faces = new Polygon[num];
			for (int i = 0; i < num; i++)
			{
				Faces[i] = new Polygon();
				Faces[i].ReadObject(input);
			}
		}
	}
	public class Polygon : IDeserializable
	{
		public VertexIndex[] VertexIndices { get; private set; }

		public void ReadObject(BinaryReaderEx input)
		{
			_ = input.Version;
			byte b = input.ReadByte();
			VertexIndices = new VertexIndex[b];
			for (int i = 0; i < b; i++)
			{
				VertexIndices[i] = input.ReadVertexIndex();
			}
		}
	}
	public class SubSkeletonIndexSet : IDeserializable
	{
		private int[] subSkeletons;

		public void ReadObject(BinaryReaderEx input)
		{
			subSkeletons = input.ReadIntArray();
		}
	}
	public class BuoyantPoint : IDeserializable
	{
		public Vector3P Coords { get; private set; }

		public float SphereRadius { get; private set; }

		public float TypicalSurface { get; private set; }

		public void ReadObject(BinaryReaderEx input)
		{
			Coords = new Vector3P(input);
			SphereRadius = input.ReadSingle();
			TypicalSurface = input.ReadSingle();
		}
	}
	public class BuoyancyType : IDeserializable
	{
		public float Volume { get; private set; }

		public void ReadObject(BinaryReaderEx input)
		{
			Volume = input.ReadSingle();
		}
	}
	public class BuoyancyTypeSpheres : BuoyancyType, IDeserializable
	{
		public int ArraySizeX { get; private set; }

		public int ArraySizeY { get; private set; }

		public int ArraySizeZ { get; private set; }

		public float StepX { get; private set; }

		public float StepY { get; private set; }

		public float StepZ { get; private set; }

		public float FullSphereRadius { get; private set; }

		public int MinSpheres { get; private set; }

		public int MaxSpheres { get; private set; }

		public BuoyantPoint[] BuoyancyPoints { get; private set; }

		public new void ReadObject(BinaryReaderEx input)
		{
			ArraySizeX = input.ReadInt32();
			ArraySizeY = input.ReadInt32();
			ArraySizeZ = input.ReadInt32();
			StepX = input.ReadSingle();
			StepY = input.ReadSingle();
			StepZ = input.ReadSingle();
			FullSphereRadius = input.ReadSingle();
			MinSpheres = input.ReadInt32();
			MaxSpheres = input.ReadInt32();
			int num = ArraySizeX * ArraySizeY * ArraySizeZ;
			BuoyancyPoints = new BuoyantPoint[num];
			for (int i = 0; i < num; i++)
			{
				BuoyancyPoints[i] = new BuoyantPoint();
				BuoyancyPoints[i].ReadObject(input);
			}
			base.ReadObject(input);
		}
	}
	public class Proxy : IDeserializable
	{
		public string proxyModel;

		public Matrix4P transformation;

		public int sequenceID;

		public int namedSelectionIndex;

		public int boneIndex;

		public int sectionIndex;

		public void ReadObject(BinaryReaderEx input)
		{
			proxyModel = input.ReadAsciiz();
			transformation = new Matrix4P(input);
			sequenceID = input.ReadInt32();
			namedSelectionIndex = input.ReadInt32();
			boneIndex = input.ReadInt32();
			if (input.Version >= 40)
			{
				sectionIndex = input.ReadInt32();
			}
		}
	}
	public class Section : IDeserializable
	{
		private int faceLowerIndex;

		private int faceUpperIndex;

		private int minBoneIndex;

		private int bonesCount;

		public short textureIndex;

		public uint special;

		public int materialIndex;

		private string mat;

		private uint nStages;

		private float[] areaOverTex;

		private bool shortIndices;

		public uint[] getFaceIndexes(Polygon[] faces)
		{
			uint num = 0u;
			uint num2 = (shortIndices ? 8u : 16u);
			uint num3 = (shortIndices ? 2u : 4u);
			List<uint> list = new List<uint>();
			for (uint num4 = 0u; num4 < faces.Length; num4++)
			{
				if (num >= faceLowerIndex && num < faceUpperIndex)
				{
					list.Add(num4);
				}
				num += num2;
				if (faces[num4].VertexIndices.Length == 4)
				{
					num += num3;
				}
				if (num >= faceUpperIndex)
				{
					break;
				}
			}
			return list.ToArray();
		}

		public void ReadObject(BinaryReaderEx input)
		{
			int version = input.Version;
			shortIndices = version < 69;
			faceLowerIndex = input.ReadInt32();
			faceUpperIndex = input.ReadInt32();
			minBoneIndex = input.ReadInt32();
			bonesCount = input.ReadInt32();
			input.ReadUInt32();
			textureIndex = input.ReadInt16();
			special = input.ReadUInt32();
			materialIndex = input.ReadInt32();
			if (materialIndex == -1)
			{
				mat = input.ReadAsciiz();
			}
			if (version >= 36)
			{
				nStages = input.ReadUInt32();
				areaOverTex = new float[nStages];
				for (int i = 0; i < nStages; i++)
				{
					areaOverTex[i] = input.ReadSingle();
				}
				if (version >= 67 && input.ReadInt32() >= 1)
				{
					(from _ in Enumerable.Range(0, 11)
						select input.ReadSingle()).ToArray();
				}
			}
			else
			{
				areaOverTex = new float[1];
				areaOverTex[0] = input.ReadSingle();
			}
		}
	}
	public class Skeleton
	{
		public string Name { get; }

		public bool isDiscrete { get; }

		public string[] bones { get; }

		public string pivotsNameObsolete { get; }

		public Skeleton(BinaryReaderEx input)
		{
			int version = input.Version;
			Name = input.ReadAsciiz();
			if (!(Name == ""))
			{
				if (version >= 23)
				{
					isDiscrete = input.ReadBoolean();
				}
				int num = input.ReadInt32();
				bones = new string[num * 2];
				for (int i = 0; i < num; i++)
				{
					bones[i * 2] = input.ReadAsciiz();
					bones[i * 2 + 1] = input.ReadAsciiz();
				}
				if (version > 40)
				{
					pivotsNameObsolete = input.ReadAsciiz();
				}
			}
		}
	}
	public abstract class STPair
	{
		public Vector3P S { get; } = new Vector3P();

		public Vector3P T { get; } = new Vector3P();
	}
	public class STPairUncompressed : STPair, IDeserializable
	{
		public void ReadObject(BinaryReaderEx input)
		{
			base.S.ReadObject(input);
			base.T.ReadObject(input);
		}
	}
	public class STPairCompressed : STPair, IDeserializable
	{
		public void ReadObject(BinaryReaderEx input)
		{
			base.S.readCompressed(input);
			base.T.readCompressed(input);
		}
	}
	public class UVSet
	{
		private bool isDiscretized;

		private float minU;

		private float minV;

		private float maxU;

		private float maxV;

		private uint nVertices;

		private bool defaultFill;

		private byte[] defaultValue;

		private byte[] uvData;

		public float[] UVData
		{
			get
			{
				float[] array = new float[nVertices * 2];
				float num = 0f;
				float num2 = 0f;
				double num3 = 1.0;
				double num4 = 1.0;
				if (isDiscretized)
				{
					num3 = maxU - minU;
					num4 = maxV - minV;
				}
				if (defaultFill)
				{
					if (isDiscretized)
					{
						num = scale(BitConverter.ToInt16(defaultValue, 0), num3, minU);
						num2 = scale(BitConverter.ToInt16(defaultValue, 2), num4, minV);
					}
					else
					{
						num = BitConverter.ToSingle(defaultValue, 0);
						num2 = BitConverter.ToSingle(defaultValue, 4);
					}
				}
				for (int i = 0; i < nVertices; i++)
				{
					if (isDiscretized)
					{
						array[i * 2] = (defaultFill ? num : scale(BitConverter.ToInt16(uvData, i * 4), num3, minU));
						array[i * 2 + 1] = (defaultFill ? num2 : scale(BitConverter.ToInt16(uvData, i * 4 + 2), num4, minV));
					}
					else
					{
						array[i * 2] = (defaultFill ? num : BitConverter.ToSingle(uvData, i * 8));
						array[i * 2 + 1] = (defaultFill ? num2 : BitConverter.ToSingle(uvData, i * 8 + 4));
					}
				}
				return array;
			}
		}

		private float scale(short value, double scale, float min)
		{
			return (float)(1.52587890625E-05 * (double)(value + 32767) * scale) + min;
		}

		public void read(BinaryReaderEx input, uint odolVersion)
		{
			isDiscretized = false;
			if (odolVersion >= 45)
			{
				isDiscretized = true;
				minU = input.ReadSingle();
				minV = input.ReadSingle();
				maxU = input.ReadSingle();
				maxV = input.ReadSingle();
			}
			nVertices = input.ReadUInt32();
			defaultFill = input.ReadBoolean();
			int num = ((odolVersion >= 45) ? 4 : 8);
			if (defaultFill)
			{
				defaultValue = input.ReadBytes(num);
			}
			else
			{
				uvData = input.ReadCompressed((uint)(nVertices * num));
			}
		}
	}
	public struct VertexIndex
	{
		private int value;

		public static implicit operator int(VertexIndex vi)
		{
			return vi.value;
		}

		public static implicit operator VertexIndex(ushort vi)
		{
			VertexIndex result = default(VertexIndex);
			result.value = ((vi == ushort.MaxValue) ? (-1) : vi);
			return result;
		}

		public static implicit operator VertexIndex(int vi)
		{
			VertexIndex result = default(VertexIndex);
			result.value = vi;
			return result;
		}
	}
	public static class VertexIndexExtensions
	{
		public static VertexIndex ReadVertexIndex(this BinaryReaderEx input)
		{
			if (input.Version >= 69)
			{
				return input.ReadInt32();
			}
			return input.ReadUInt16();
		}

		public static VertexIndex[] ReadCompressedVertexIndexArray(this BinaryReaderEx input)
		{
			if (input.Version >= 69)
			{
				return input.ReadCompressedArray((Func<BinaryReaderEx, VertexIndex>)((BinaryReaderEx i) => i.ReadInt32()), 4);
			}
			return input.ReadCompressedArray((Func<BinaryReaderEx, VertexIndex>)((BinaryReaderEx i) => i.ReadUInt16()), 2);
		}
	}
}
namespace BisDll.Model.MLOD
{
	public class Face
	{
		public int NumberOfVertices { get; private set; }

		public Vertex[] Vertices { get; private set; }

		public FaceFlags Flags { get; private set; }

		public string Texture { get; private set; }

		public string Material { get; private set; }

		public Face(int nVerts, Vertex[] verts, FaceFlags flags, string texture, string material)
		{
			NumberOfVertices = nVerts;
			Vertices = verts;
			Flags = flags;
			Texture = texture;
			Material = material;
		}

		public Face(BinaryReaderEx input)
		{
			read(input);
		}

		public void read(BinaryReaderEx input)
		{
			NumberOfVertices = input.ReadInt32();
			Vertices = new Vertex[4];
			for (int i = 0; i < 4; i++)
			{
				Vertices[i] = new Vertex(input);
			}
			Flags = (FaceFlags)input.ReadInt32();
			Texture = input.ReadAsciiz();
			Material = input.ReadAsciiz();
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(NumberOfVertices);
			for (int i = 0; i < 4; i++)
			{
				if (i < Vertices.Length && Vertices[i] != null)
				{
					Vertices[i].write(output);
					continue;
				}
				output.Write(0);
				output.Write(0);
				output.Write(0);
				output.Write(0);
			}
			output.Write((int)Flags);
			output.writeAsciiz(Texture);
			output.writeAsciiz(Material);
		}
	}
	public class MLOD_LOD : P3D_LOD
	{
		public uint unk1;

		public Point[] points;

		public Vector3P[] normals;

		public Face[] faces;

		public List<Tagg> taggs;

		public override Vector3P[] Points => points;

		public override Vector3P[] Normals => normals;

		public override string[] Textures => faces.Select((Face f) => f.Texture).Distinct().ToArray();

		public override string[] MaterialNames => faces.Select((Face f) => f.Material).Distinct().ToArray();

		public MLOD_LOD()
		{
		}

		public MLOD_LOD(float resolution)
		{
			base.resolution = resolution;
		}

		private Tagg readTagg(BinaryReaderEx input)
		{
			Tagg tagg = new MassTagg();
			if (!input.ReadBoolean())
			{
				throw new Exception("Deactivated Tagg?");
			}
			tagg.Name = input.ReadAsciiz();
			tagg.DataSize = input.ReadUInt32();
			switch (tagg.Name)
			{
			case "#SharpEdges#":
			{
				SharpEdgesTagg sharpEdgesTagg = new SharpEdgesTagg();
				sharpEdgesTagg.Name = "#SharpEdges#";
				sharpEdgesTagg.DataSize = tagg.DataSize;
				sharpEdgesTagg.read(input);
				return sharpEdgesTagg;
			}
			case "#Property#":
			{
				PropertyTagg propertyTagg = new PropertyTagg();
				propertyTagg.Name = "#Property#";
				propertyTagg.DataSize = tagg.DataSize;
				propertyTagg.read(input);
				return propertyTagg;
			}
			case "#Mass#":
			{
				MassTagg massTagg = new MassTagg();
				massTagg.Name = "#Mass#";
				massTagg.DataSize = tagg.DataSize;
				massTagg.read(input);
				return massTagg;
			}
			case "#UVSet#":
			{
				UVSetTagg uVSetTagg = new UVSetTagg();
				uVSetTagg.Name = "#UVSet#";
				uVSetTagg.DataSize = tagg.DataSize;
				uVSetTagg.read(input, faces);
				return uVSetTagg;
			}
			case "#Lock#":
			{
				LockTagg lockTagg = new LockTagg();
				lockTagg.Name = "#Lock#";
				lockTagg.DataSize = tagg.DataSize;
				lockTagg.read(input, Points.Length, faces.Length);
				return lockTagg;
			}
			case "#Selected#":
			{
				SelectedTagg selectedTagg = new SelectedTagg();
				selectedTagg.Name = "#Selected#";
				selectedTagg.DataSize = tagg.DataSize;
				selectedTagg.read(input, Points.Length, faces.Length);
				return selectedTagg;
			}
			case "#Animation#":
			{
				AnimationTagg animationTagg = new AnimationTagg();
				animationTagg.Name = "#Animation#";
				animationTagg.DataSize = tagg.DataSize;
				animationTagg.read(input);
				return animationTagg;
			}
			case "#EndOfFile#":
				return tagg;
			default:
			{
				NamedSelectionTagg namedSelectionTagg = new NamedSelectionTagg();
				namedSelectionTagg.Name = tagg.Name;
				namedSelectionTagg.DataSize = tagg.DataSize;
				namedSelectionTagg.read(input, Points.Length, faces.Length);
				return namedSelectionTagg;
			}
			}
		}

		private void writeTagg(BisDll.Stream.BinaryWriter output, Tagg tagg)
		{
			switch (tagg.Name)
			{
			case "#SharpEdges#":
				((SharpEdgesTagg)tagg).write(output);
				break;
			case "#Property#":
				((PropertyTagg)tagg).write(output);
				break;
			case "#Mass#":
				((MassTagg)tagg).write(output);
				break;
			case "#UVSet#":
				((UVSetTagg)tagg).write(output);
				break;
			case "#Lock#":
				((LockTagg)tagg).write(output);
				break;
			case "#Selected#":
				((SelectedTagg)tagg).write(output);
				break;
			case "#Animation#":
				((AnimationTagg)tagg).write(output);
				break;
			default:
				((NamedSelectionTagg)tagg).write(output);
				break;
			case "#EndOfFile#":
				break;
			}
		}

		public void read(BinaryReaderEx input)
		{
			if (input.ReadAscii(4) != "P3DM")
			{
				throw new Exception("Only P3DM LODs are supported");
			}
			if (input.ReadUInt32() != 28 || input.ReadUInt32() != 256)
			{
				throw new Exception("Unknown P3DM version");
			}
			uint num = input.ReadUInt32();
			uint num2 = input.ReadUInt32();
			uint num3 = input.ReadUInt32();
			unk1 = input.ReadUInt32();
			points = new Point[num];
			normals = new Vector3P[num2];
			faces = new Face[num3];
			for (int i = 0; i < num; i++)
			{
				points[i] = new Point(input);
			}
			for (int j = 0; j < num2; j++)
			{
				normals[j] = new Vector3P(input);
			}
			for (int k = 0; k < num3; k++)
			{
				faces[k] = new Face(input);
			}
			if (input.ReadAscii(4) != "TAGG")
			{
				throw new Exception("TAGG expected");
			}
			taggs = new List<Tagg>();
			Tagg tagg;
			do
			{
				tagg = readTagg(input);
				if (tagg.Name != "#EndOfFile#")
				{
					taggs.Add(tagg);
				}
			}
			while (tagg.Name != "#EndOfFile#");
			resolution = input.ReadSingle();
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			int num = points.Length;
			int num2 = normals.Length;
			int num3 = faces.Length;
			output.writeAscii("P3DM", 4u);
			output.Write(28);
			output.Write(256);
			output.Write(num);
			output.Write(num2);
			output.Write(num3);
			output.Write(unk1);
			for (int i = 0; i < num; i++)
			{
				points[i].write(output);
			}
			for (int j = 0; j < num2; j++)
			{
				normals[j].write(output);
			}
			for (int k = 0; k < num3; k++)
			{
				faces[k].write(output);
			}
			output.writeAscii("TAGG", 4u);
			foreach (Tagg tagg in taggs)
			{
				writeTagg(output, tagg);
			}
			output.Write(value: true);
			output.writeAsciiz("#EndOfFile#");
			output.Write(0);
			output.Write(resolution);
		}

		public float getHeight()
		{
			int num = Points.Length;
			if ((long)num <= 1L)
			{
				return 0f;
			}
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			for (int i = 0; i < num; i++)
			{
				float y = points[i].Y;
				if (y > num3)
				{
					num3 = y;
				}
				if (y < num2)
				{
					num2 = y;
				}
			}
			return num3 - num2;
		}
	}
	public class MLOD : P3D
	{
		private MLOD_LOD[] lods;

		public override P3D_LOD[] LODs => lods;

		public override float Mass
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public MLOD(string fileName)
		{
			byte[] array = File.ReadAllBytes(fileName);
			BinaryReaderEx binaryReaderEx = new BinaryReaderEx(new MemoryStream(array, 0, array.Length, writable: false, publiclyVisible: true));
			read(binaryReaderEx);
			binaryReaderEx.Close();
		}

		public MLOD(System.IO.Stream stream)
		{
			read(new BinaryReaderEx(stream));
		}

		public MLOD(MLOD_LOD[] lods)
		{
			this.lods = lods;
		}

		private void read(BinaryReaderEx input)
		{
			if (input.ReadAscii(4) != "MLOD")
			{
				throw new Exception("MLOD signature expected");
			}
			base.Version = input.ReadUInt32();
			if (base.Version != 257)
			{
				throw new Exception("Unknown MLOD version");
			}
			uint num = input.ReadUInt32();
			lods = new MLOD_LOD[num];
			for (int i = 0; i < num; i++)
			{
				lods[i] = new MLOD_LOD();
				lods[i].read(input);
			}
		}

		private void write(BisDll.Stream.BinaryWriter output)
		{
			output.writeAscii("MLOD", 4u);
			output.Write(257);
			output.Write(lods.Length);
			for (int i = 0; i < lods.Length; i++)
			{
				lods[i].write(output);
			}
		}

		public void writeToFile(string file, bool allowOverwriting = false)
		{
			FileMode mode = ((!allowOverwriting) ? FileMode.CreateNew : FileMode.Create);
			BisDll.Stream.BinaryWriter binaryWriter = new BisDll.Stream.BinaryWriter(new FileStream(file, mode));
			write(binaryWriter);
			binaryWriter.Close();
		}

		public MemoryStream writeToMemory()
		{
			MemoryStream memoryStream = new MemoryStream(100000);
			BisDll.Stream.BinaryWriter binaryWriter = new BisDll.Stream.BinaryWriter(memoryStream);
			write(binaryWriter);
			binaryWriter.Position = 0L;
			return memoryStream;
		}

		public void writeToStream(System.IO.Stream stream)
		{
			BisDll.Stream.BinaryWriter output = new BisDll.Stream.BinaryWriter(stream);
			write(output);
		}
	}
	public class Point : Vector3P
	{
		public PointFlags PointFlags { get; private set; }

		public Point(Vector3P pos, PointFlags flags)
			: base(pos.X, pos.Y, pos.Z)
		{
			PointFlags = flags;
		}

		public Point(BinaryReaderEx input)
			: base(input)
		{
			PointFlags = (PointFlags)input.ReadUInt32();
		}

		public new void write(BisDll.Stream.BinaryWriter output)
		{
			base.write(output);
			output.Write((uint)PointFlags);
		}
	}
	public class Vertex
	{
		public int PointIndex { get; private set; }

		public int NormalIndex { get; private set; }

		public float U { get; private set; }

		public float V { get; private set; }

		public Vertex(BinaryReaderEx input)
		{
			read(input);
		}

		public Vertex(int point, int normal, float u, float v)
		{
			PointIndex = point;
			NormalIndex = normal;
			U = u;
			V = v;
		}

		public void read(BinaryReaderEx input)
		{
			PointIndex = input.ReadInt32();
			NormalIndex = input.ReadInt32();
			U = input.ReadSingle();
			V = input.ReadSingle();
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(PointIndex);
			output.Write(NormalIndex);
			output.Write(U);
			output.Write(V);
		}
	}
	public abstract class Tagg
	{
		public uint DataSize { get; set; }

		public string Name { get; set; }
	}
	public class AnimationTagg : Tagg
	{
		public float frameTime;

		public Vector3P[] framePoints;

		public void read(BinaryReaderEx input)
		{
			uint num = (base.DataSize - 4) / 12;
			frameTime = input.ReadSingle();
			framePoints = new Vector3P[num];
			for (int i = 0; i < num; i++)
			{
				framePoints[i] = new Vector3P(input);
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			output.Write(frameTime);
			for (int i = 0; i < framePoints.Length; i++)
			{
				framePoints[i].write(output);
			}
		}
	}
	public class LockTagg : Tagg
	{
		public bool[] lockedPoints;

		public bool[] lockedFaces;

		public void read(BinaryReaderEx input, int nPoints, int nFaces)
		{
			lockedPoints = new bool[nPoints];
			for (int i = 0; i < nPoints; i++)
			{
				lockedPoints[i] = input.ReadBoolean();
			}
			lockedFaces = new bool[nFaces];
			for (int j = 0; j < nFaces; j++)
			{
				lockedFaces[j] = input.ReadBoolean();
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			for (int i = 0; i < lockedPoints.Length; i++)
			{
				output.Write(lockedPoints[i]);
			}
			for (int j = 0; j < lockedFaces.Length; j++)
			{
				output.Write(lockedFaces[j]);
			}
		}
	}
	public class MassTagg : Tagg
	{
		public float[] mass;

		public void read(BinaryReaderEx input)
		{
			uint num = base.DataSize / 4;
			mass = new float[num];
			for (int i = 0; i < num; i++)
			{
				mass[i] = input.ReadSingle();
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			uint num = base.DataSize / 4;
			for (int i = 0; i < num; i++)
			{
				output.Write(mass[i]);
			}
		}
	}
	public class NamedSelectionTagg : Tagg
	{
		public byte[] points;

		public byte[] faces;

		public void read(BinaryReaderEx input, int nPoints, int nFaces)
		{
			points = new byte[nPoints];
			for (int i = 0; i < nPoints; i++)
			{
				points[i] = input.ReadByte();
			}
			faces = new byte[nFaces];
			for (int j = 0; j < nFaces; j++)
			{
				faces[j] = input.ReadByte();
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			for (int i = 0; i < points.Length; i++)
			{
				output.Write(points[i]);
			}
			for (int j = 0; j < faces.Length; j++)
			{
				output.Write(faces[j]);
			}
		}
	}
	public class PropertyTagg : Tagg
	{
		public string name;

		public string value;

		public void read(BinaryReaderEx input)
		{
			name = input.ReadAscii(64);
			value = input.ReadAscii(64);
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			output.writeAscii(name, 64u);
			output.writeAscii(value, 64u);
		}
	}
	public class SelectedTagg : Tagg
	{
		public byte[] weightedPoints;

		public byte[] faces;

		public void read(BinaryReaderEx input, int nPoints, int nFaces)
		{
			weightedPoints = new byte[nPoints];
			for (int i = 0; i < nPoints; i++)
			{
				weightedPoints[i] = input.ReadByte();
			}
			faces = new byte[nFaces];
			for (int j = 0; j < nFaces; j++)
			{
				faces[j] = input.ReadByte();
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			for (int i = 0; i < weightedPoints.Length; i++)
			{
				output.Write(weightedPoints[i]);
			}
			for (int j = 0; j < faces.Length; j++)
			{
				output.Write(faces[j]);
			}
		}
	}
	public class SharpEdgesTagg : Tagg
	{
		public uint[,] pointIndices;

		public void read(BinaryReaderEx input)
		{
			uint num = base.DataSize / 8;
			pointIndices = new uint[(int)(IntPtr)num, 2];
			for (int i = 0; i < num; i++)
			{
				pointIndices[i, 0] = input.ReadUInt32();
				pointIndices[i, 1] = input.ReadUInt32();
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			uint num = base.DataSize / 8;
			for (int i = 0; i < num; i++)
			{
				output.Write(pointIndices[i, 0]);
				output.Write(pointIndices[i, 1]);
			}
		}
	}
	public class UVSetTagg : Tagg
	{
		public uint uvSetNr;

		public float[][,] faceUVs;

		public void read(BinaryReaderEx input, Face[] faces)
		{
			uvSetNr = input.ReadUInt32();
			faceUVs = new float[faces.Length][,];
			for (int i = 0; i < faces.Length; i++)
			{
				faceUVs[i] = new float[faces[i].NumberOfVertices, 2];
				for (int j = 0; j < faces[i].NumberOfVertices; j++)
				{
					faceUVs[i][j, 0] = input.ReadSingle();
					faceUVs[i][j, 1] = input.ReadSingle();
				}
			}
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(value: true);
			output.writeAsciiz(base.Name);
			output.Write(base.DataSize);
			output.Write(uvSetNr);
			for (int i = 0; i < faceUVs.Length; i++)
			{
				for (int j = 0; j < faceUVs[i].Length / 2; j++)
				{
					output.Write(faceUVs[i][j, 0]);
					output.Write(faceUVs[i][j, 1]);
				}
			}
		}
	}
}
namespace BisDll.Compression
{
	public static class LZO
	{
		private static readonly uint M2_MAX_OFFSET = 2048u;

		public unsafe static uint decompress(byte* input, byte* output, uint expectedSize)
		{
			byte* ptr = output + expectedSize;
			byte* ptr2 = output;
			byte* ptr3 = input;
			if (*ptr3 <= 17)
			{
				goto IL_0050;
			}
			uint num = (uint)(*(ptr3++) - 17);
			if (num >= 4)
			{
				if (ptr - ptr2 < num)
				{
					throw new OverflowException("Outpur Overrun");
				}
				do
				{
					*(ptr2++) = *(ptr3++);
				}
				while (--num != 0);
				goto IL_00f2;
			}
			goto IL_037d;
			IL_037d:
			if (ptr - ptr2 < num)
			{
				throw new OverflowException("Output Overrun");
			}
			*(ptr2++) = *(ptr3++);
			if (num > 1)
			{
				*(ptr2++) = *(ptr3++);
				if (num > 2)
				{
					*(ptr2++) = *(ptr3++);
				}
			}
			num = *(ptr3++);
			goto IL_0169;
			IL_0169:
			byte* ptr4;
			if (num >= 64)
			{
				ptr4 = ptr2 - 1;
				ptr4 -= (num >> 2) & 7;
				ptr4 -= *(ptr3++) << 3;
				num = (num >> 5) - 1;
				if (ptr4 < output || ptr4 >= ptr2)
				{
					throw new OverflowException("Lookbehind Overrun");
				}
				if (ptr - ptr2 < num + 2)
				{
					throw new OverflowException("Output Overrun");
				}
			}
			else
			{
				if (num >= 32)
				{
					num &= 0x1F;
					if (num == 0)
					{
						for (; *ptr3 == 0; ptr3++)
						{
							num += 255;
						}
						num += (uint)(31 + *(ptr3++));
					}
					ptr4 = ptr2 - 1;
					ptr4 -= (*ptr3 >> 2) + (ptr3[1] << 6);
					ptr3 += 2;
				}
				else
				{
					if (num < 16)
					{
						ptr4 = ptr2 - 1;
						ptr4 -= num >> 2;
						ptr4 -= *(ptr3++) << 2;
						if (ptr4 < output || ptr4 >= ptr2)
						{
							throw new OverflowException("Lookbehind Overrun");
						}
						if (ptr - ptr2 < 2)
						{
							throw new OverflowException("Output Overrun");
						}
						*(ptr2++) = *(ptr4++);
						*(ptr2++) = *ptr4;
						goto IL_036f;
					}
					ptr4 = ptr2;
					ptr4 -= (num & 8) << 11;
					num &= 7;
					if (num == 0)
					{
						for (; *ptr3 == 0; ptr3++)
						{
							num += 255;
						}
						num += (uint)(7 + *(ptr3++));
					}
					ptr4 -= (*ptr3 >> 2) + (ptr3[1] << 6);
					ptr3 += 2;
					if (ptr4 == ptr2)
					{
						_ = ptr2 - output;
						if (ptr4 != ptr)
						{
							throw new OverflowException("Output Underrun");
						}
						return (uint)(ptr3 - input);
					}
					ptr4 -= 16384;
				}
				if (ptr4 < output || ptr4 >= ptr2)
				{
					throw new OverflowException("Lookbehind Overrun");
				}
				if (ptr - ptr2 < num + 2)
				{
					throw new OverflowException("Output Overrun");
				}
				if (num >= 6 && ptr2 - ptr4 >= 4)
				{
					*(int*)ptr2 = *(int*)ptr4;
					ptr2 += 4;
					ptr4 += 4;
					num -= 2;
					do
					{
						*(int*)ptr2 = *(int*)ptr4;
						ptr2 += 4;
						ptr4 += 4;
						num -= 4;
					}
					while (num >= 4);
					if (num != 0)
					{
						do
						{
							*(ptr2++) = *(ptr4++);
						}
						while (--num != 0);
					}
					goto IL_036f;
				}
			}
			*(ptr2++) = *(ptr4++);
			*(ptr2++) = *(ptr4++);
			do
			{
				*(ptr2++) = *(ptr4++);
			}
			while (--num != 0);
			goto IL_036f;
			IL_036f:
			num = (uint)(ptr3[-2] & 3);
			if (num == 0)
			{
				goto IL_0050;
			}
			goto IL_037d;
			IL_00f2:
			num = *(ptr3++);
			if (num >= 16)
			{
				goto IL_0169;
			}
			ptr4 = ptr2 - (1 + M2_MAX_OFFSET);
			ptr4 -= num >> 2;
			ptr4 -= *(ptr3++) << 2;
			if (ptr4 < output || ptr4 >= ptr2)
			{
				throw new OverflowException("Lookbehind Overrun");
			}
			if (ptr - ptr2 < 3)
			{
				throw new OverflowException("Output Overrun");
			}
			*(ptr2++) = *(ptr4++);
			*(ptr2++) = *(ptr4++);
			*(ptr2++) = *ptr4;
			goto IL_036f;
			IL_0050:
			num = *(ptr3++);
			if (num < 16)
			{
				if (num == 0)
				{
					for (; *ptr3 == 0; ptr3++)
					{
						num += 255;
					}
					num += (uint)(15 + *(ptr3++));
				}
				if (ptr - ptr2 < num + 3)
				{
					throw new OverflowException("Output Overrun");
				}
				*(int*)ptr2 = *(int*)ptr3;
				ptr2 += 4;
				ptr3 += 4;
				if (--num != 0)
				{
					if (num >= 4)
					{
						do
						{
							*(int*)ptr2 = *(int*)ptr3;
							ptr2 += 4;
							ptr3 += 4;
							num -= 4;
						}
						while (num >= 4);
						if (num != 0)
						{
							do
							{
								*(ptr2++) = *(ptr3++);
							}
							while (--num != 0);
						}
					}
					else
					{
						do
						{
							*(ptr2++) = *(ptr3++);
						}
						while (--num != 0);
					}
				}
				goto IL_00f2;
			}
			goto IL_0169;
		}

		private static byte ip(System.IO.Stream i)
		{
			byte result = (byte)i.ReadByte();
			i.Position--;
			return result;
		}

		private static byte ip(System.IO.Stream i, short offset)
		{
			i.Position += offset;
			byte result = (byte)i.ReadByte();
			i.Position -= offset + 1;
			return result;
		}

		private static byte next(System.IO.Stream i)
		{
			return (byte)i.ReadByte();
		}

		public unsafe static uint decompress(System.IO.Stream i, byte* output, uint expectedSize)
		{
			long position = i.Position;
			byte* ptr = output + expectedSize;
			byte* ptr2 = output;
			if (ip(i) <= 17)
			{
				goto IL_0059;
			}
			uint num = (uint)(next(i) - 17);
			if (num >= 4)
			{
				if (ptr - ptr2 < num)
				{
					throw new OverflowException("Outpur Overrun");
				}
				do
				{
					*(ptr2++) = next(i);
				}
				while (--num != 0);
				goto IL_0156;
			}
			goto IL_0435;
			IL_0435:
			if (ptr - ptr2 < num)
			{
				throw new OverflowException("Output Overrun");
			}
			*(ptr2++) = next(i);
			if (num > 1)
			{
				*(ptr2++) = next(i);
				if (num > 2)
				{
					*(ptr2++) = next(i);
				}
			}
			num = next(i);
			goto IL_01cd;
			IL_01cd:
			byte* ptr3;
			if (num >= 64)
			{
				ptr3 = ptr2 - 1;
				ptr3 -= (num >> 2) & 7;
				ptr3 -= next(i) << 3;
				num = (num >> 5) - 1;
				if (ptr3 < output || ptr3 >= ptr2)
				{
					throw new OverflowException("Lookbehind Overrun");
				}
				if (ptr - ptr2 < num + 2)
				{
					throw new OverflowException("Output Overrun");
				}
			}
			else
			{
				if (num >= 32)
				{
					num &= 0x1F;
					if (num == 0)
					{
						while (ip(i) == 0)
						{
							num += 255;
							i.Position++;
						}
						num += (uint)(31 + next(i));
					}
					ptr3 = ptr2 - 1;
					ptr3 -= (ip(i, 0) >> 2) + (ip(i, 1) << 6);
					i.Position += 2L;
				}
				else
				{
					if (num < 16)
					{
						ptr3 = ptr2 - 1;
						ptr3 -= num >> 2;
						ptr3 -= next(i) << 2;
						if (ptr3 < output || ptr3 >= ptr2)
						{
							throw new OverflowException("Lookbehind Overrun");
						}
						if (ptr - ptr2 < 2)
						{
							throw new OverflowException("Output Overrun");
						}
						*(ptr2++) = *(ptr3++);
						*(ptr2++) = *ptr3;
						goto IL_0424;
					}
					ptr3 = ptr2;
					ptr3 -= (num & 8) << 11;
					num &= 7;
					if (num == 0)
					{
						while (ip(i) == 0)
						{
							num += 255;
							i.Position++;
						}
						num += (uint)(7 + next(i));
					}
					ptr3 -= (ip(i, 0) >> 2) + (ip(i, 1) << 6);
					i.Position += 2L;
					if (ptr3 == ptr2)
					{
						_ = ptr2 - output;
						if (ptr3 != ptr)
						{
							throw new OverflowException("Output Underrun");
						}
						return (uint)(i.Position - position);
					}
					ptr3 -= 16384;
				}
				if (ptr3 < output || ptr3 >= ptr2)
				{
					throw new OverflowException("Lookbehind Overrun");
				}
				if (ptr - ptr2 < num + 2)
				{
					throw new OverflowException("Output Overrun");
				}
				if (num >= 6 && ptr2 - ptr3 >= 4)
				{
					*(int*)ptr2 = *(int*)ptr3;
					ptr2 += 4;
					ptr3 += 4;
					num -= 2;
					do
					{
						*(int*)ptr2 = *(int*)ptr3;
						ptr2 += 4;
						ptr3 += 4;
						num -= 4;
					}
					while (num >= 4);
					if (num != 0)
					{
						do
						{
							*(ptr2++) = *(ptr3++);
						}
						while (--num != 0);
					}
					goto IL_0424;
				}
			}
			*(ptr2++) = *(ptr3++);
			*(ptr2++) = *(ptr3++);
			do
			{
				*(ptr2++) = *(ptr3++);
			}
			while (--num != 0);
			goto IL_0424;
			IL_0424:
			num = (uint)(ip(i, -2) & 3);
			if (num == 0)
			{
				goto IL_0059;
			}
			goto IL_0435;
			IL_0156:
			num = next(i);
			if (num >= 16)
			{
				goto IL_01cd;
			}
			ptr3 = ptr2 - (1 + M2_MAX_OFFSET);
			ptr3 -= num >> 2;
			ptr3 -= next(i) << 2;
			if (ptr3 < output || ptr3 >= ptr2)
			{
				throw new OverflowException("Lookbehind Overrun");
			}
			if (ptr - ptr2 < 3)
			{
				throw new OverflowException("Output Overrun");
			}
			*(ptr2++) = *(ptr3++);
			*(ptr2++) = *(ptr3++);
			*(ptr2++) = *ptr3;
			goto IL_0424;
			IL_0059:
			num = next(i);
			if (num < 16)
			{
				if (num == 0)
				{
					while (ip(i) == 0)
					{
						num += 255;
						i.Position++;
					}
					num += (uint)(15 + next(i));
				}
				if (ptr - ptr2 < num + 3)
				{
					throw new OverflowException("Output Overrun");
				}
				*(ptr2++) = next(i);
				*(ptr2++) = next(i);
				*(ptr2++) = next(i);
				*(ptr2++) = next(i);
				if (--num != 0)
				{
					if (num >= 4)
					{
						do
						{
							*(ptr2++) = next(i);
							*(ptr2++) = next(i);
							*(ptr2++) = next(i);
							*(ptr2++) = next(i);
							num -= 4;
						}
						while (num >= 4);
						if (num != 0)
						{
							do
							{
								*(ptr2++) = next(i);
							}
							while (--num != 0);
						}
					}
					else
					{
						do
						{
							*(ptr2++) = next(i);
						}
						while (--num != 0);
					}
				}
				goto IL_0156;
			}
			goto IL_01cd;
		}

		public unsafe static uint readLZO(System.IO.Stream input, out byte[] dst, uint expectedSize)
		{
			dst = new byte[expectedSize];
			fixed (byte* output = &dst[0])
			{
				return decompress(input, output, expectedSize);
			}
		}

		public unsafe static byte[] readLZO(System.IO.Stream input, uint expectedSize)
		{
			byte[] array = new byte[expectedSize];
			fixed (byte* output = &array[0])
			{
				decompress(input, output, expectedSize);
			}
			return array;
		}
	}
	public static class LZSS
	{
		public static uint readLZSS(System.IO.Stream input, out byte[] dst, uint expectedSize, bool useSignedChecksum)
		{
			char[] array = new char[4113];
			dst = new byte[expectedSize];
			if (expectedSize == 0)
			{
				return 0u;
			}
			long position = input.Position;
			uint num = expectedSize;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < 4078; i++)
			{
				array[i] = ' ';
			}
			int num4 = 4078;
			int num5 = 0;
			while (num != 0)
			{
				if (((num5 >>= 1) & 0x100) == 0)
				{
					int num6 = input.ReadByte();
					num5 = num6 | 0xFF00;
				}
				if ((num5 & 1) != 0)
				{
					int num6 = input.ReadByte();
					num3 = ((!useSignedChecksum) ? (num3 + (byte)num6) : (num3 + (sbyte)num6));
					dst[num2++] = (byte)num6;
					num--;
					array[num4] = (char)num6;
					num4++;
					num4 &= 0xFFF;
					continue;
				}
				int i = input.ReadByte();
				int num7 = input.ReadByte();
				i |= (num7 & 0xF0) << 4;
				num7 &= 0xF;
				num7 += 2;
				int j = num4 - i;
				int num8 = num7 + j;
				if (num7 + 1 > num)
				{
					throw new ArgumentException("LZSS overflow");
				}
				for (; j <= num8; j++)
				{
					int num6 = (byte)array[j & 0xFFF];
					num3 = ((!useSignedChecksum) ? (num3 + (byte)num6) : (num3 + (sbyte)num6));
					dst[num2++] = (byte)num6;
					num--;
					array[num4] = (char)num6;
					num4++;
					num4 &= 0xFFF;
				}
			}
			byte[] array2 = new byte[4];
			input.Read(array2, 0, 4);
			if (BitConverter.ToInt32(array2, 0) != num3)
			{
				throw new ArgumentException("Checksum mismatch");
			}
			return (uint)(input.Position - position);
		}
	}
}
namespace BisDll.Common
{
	public struct ColorP
	{
		public float Red { get; private set; }

		public float Green { get; private set; }

		public float Blue { get; private set; }

		public float Alpha { get; private set; }

		public ColorP(float r, float g, float b, float a)
		{
			Red = r;
			Green = g;
			Blue = b;
			Alpha = a;
		}

		public ColorP(BinaryReaderEx input)
		{
			Red = input.ReadSingle();
			Green = input.ReadSingle();
			Blue = input.ReadSingle();
			Alpha = input.ReadSingle();
		}

		public void read(BinaryReaderEx input)
		{
			Red = input.ReadSingle();
			Green = input.ReadSingle();
			Blue = input.ReadSingle();
			Alpha = input.ReadSingle();
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(Red);
			output.Write(Green);
			output.Write(Blue);
			output.Write(Alpha);
		}

		public override string ToString()
		{
			CultureInfo cultureInfo = new CultureInfo("en-GB");
			return "{" + Red.ToString(cultureInfo.NumberFormat) + "," + Green.ToString(cultureInfo.NumberFormat) + "," + Blue.ToString(cultureInfo.NumberFormat) + "," + Alpha.ToString(cultureInfo.NumberFormat) + "}";
		}
	}
	public struct PackedColor
	{
		private uint value;

		public byte A8 => (byte)((value >> 24) & 0xFF);

		public byte R8 => (byte)((value >> 16) & 0xFF);

		public byte G8 => (byte)((value >> 8) & 0xFF);

		public byte B8 => (byte)(value & 0xFF);

		public PackedColor(uint value)
		{
			this.value = value;
		}

		public PackedColor(byte r, byte g, byte b, byte a = byte.MaxValue)
		{
			value = PackColor(r, g, b, a);
		}

		public PackedColor(float r, float g, float b, float a)
		{
			byte r2 = (byte)(r * 255f);
			byte g2 = (byte)(g * 255f);
			byte b2 = (byte)(b * 255f);
			byte a2 = (byte)(a * 255f);
			value = PackColor(r2, g2, b2, a2);
		}

		internal static uint PackColor(byte r, byte g, byte b, byte a)
		{
			return (uint)((a << 24) | (r << 16) | (g << 8) | b);
		}
	}
}
namespace BisDll.Common.Math
{
	public class Matrix3P
	{
		private Vector3P[] columns;

		public Vector3P Aside => columns[0];

		public Vector3P Up => columns[1];

		public Vector3P Dir => columns[2];

		public Vector3P this[int col] => columns[col];

		public float this[int row, int col]
		{
			get
			{
				return this[col][row];
			}
			set
			{
				this[col][row] = value;
			}
		}

		public Matrix3P()
			: this(0f)
		{
		}

		public Matrix3P(float val)
			: this(new Vector3P(val), new Vector3P(val), new Vector3P(val))
		{
		}

		public Matrix3P(BinaryReaderEx input)
			: this(new Vector3P(input), new Vector3P(input), new Vector3P(input))
		{
		}

		private Matrix3P(Vector3P aside, Vector3P up, Vector3P dir)
		{
			columns = new Vector3P[3] { aside, up, dir };
		}

		public static Matrix3P operator -(Matrix3P a)
		{
			return new Matrix3P(-a.Aside, -a.Up, -a.Dir);
		}

		public static Matrix3P operator *(Matrix3P a, Matrix3P b)
		{
			Matrix3P matrix3P = new Matrix3P();
			float num = b[0, 0];
			float num2 = b[1, 0];
			float num3 = b[2, 0];
			matrix3P[0, 0] = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3;
			matrix3P[1, 0] = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3;
			matrix3P[2, 0] = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3;
			num = b[0, 1];
			num2 = b[1, 1];
			num3 = b[2, 1];
			matrix3P[0, 1] = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3;
			matrix3P[1, 1] = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3;
			matrix3P[2, 1] = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3;
			num = b[0, 2];
			num2 = b[1, 2];
			num3 = b[2, 2];
			matrix3P[0, 2] = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3;
			matrix3P[1, 2] = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3;
			matrix3P[2, 2] = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3;
			return matrix3P;
		}

		public void setTilda(Vector3P a)
		{
			Aside.Y = 0f - a.Z;
			Aside.Z = a.Y;
			Up.X = a.Z;
			Up.Z = 0f - a.X;
			Dir.X = 0f - a.Y;
			Dir.Y = a.X;
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			Aside.write(output);
			Up.write(output);
			Dir.write(output);
		}
	}
	public class Matrix4P
	{
		private Matrix3P orientation;

		private Vector3P position;

		public Matrix3P Orientation => orientation;

		public Vector3P Position => position;

		public float this[int row, int col]
		{
			get
			{
				if (col != 3)
				{
					return orientation[col][row];
				}
				return position[row];
			}
			set
			{
				if (col == 3)
				{
					position[row] = value;
				}
				else
				{
					orientation[col][row] = value;
				}
			}
		}

		public Matrix4P()
			: this(0f)
		{
		}

		public Matrix4P(float val)
			: this(new Matrix3P(val), new Vector3P(val))
		{
		}

		public Matrix4P(BinaryReaderEx input)
			: this(new Matrix3P(input), new Vector3P(input))
		{
		}

		private Matrix4P(Matrix3P orientation, Vector3P position)
		{
			this.orientation = orientation;
			this.position = position;
		}

		public static Matrix4P operator *(Matrix4P a, Matrix4P b)
		{
			Matrix4P matrix4P = new Matrix4P();
			float num = b[0, 0];
			float num2 = b[1, 0];
			float num3 = b[2, 0];
			matrix4P[0, 0] = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3;
			matrix4P[1, 0] = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3;
			matrix4P[2, 0] = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3;
			num = b[0, 1];
			num2 = b[1, 1];
			num3 = b[2, 1];
			matrix4P[0, 1] = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3;
			matrix4P[1, 1] = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3;
			matrix4P[2, 1] = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3;
			num = b[0, 2];
			num2 = b[1, 2];
			num3 = b[2, 2];
			matrix4P[0, 2] = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3;
			matrix4P[1, 2] = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3;
			matrix4P[2, 2] = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3;
			num = b.Position.X;
			num2 = b.Position.Y;
			num3 = b.Position.Z;
			matrix4P.Position.X = a[0, 0] * num + a[0, 1] * num2 + a[0, 2] * num3 + a.Position.X;
			matrix4P.Position.Y = a[1, 0] * num + a[1, 1] * num2 + a[1, 2] * num3 + a.Position.Y;
			matrix4P.Position.Z = a[2, 0] * num + a[2, 1] * num2 + a[2, 2] * num3 + a.Position.Z;
			return matrix4P;
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			orientation.write(output);
			position.write(output);
		}
	}
	public class Quaternion
	{
		private float x;

		private float y;

		private float z;

		private float w;

		public float X => x;

		public float Y => x;

		public float Z => x;

		public float W => x;

		public Quaternion Inverse
		{
			get
			{
				normalize();
				return Conjugate;
			}
		}

		public Quaternion Conjugate => new Quaternion(0f - x, 0f - y, 0f - z, w);

		public static Quaternion readCompressed(byte[] data)
		{
			float num = (float)((double)(-BitConverter.ToInt16(data, 0)) / 16384.0);
			float num2 = (float)((double)BitConverter.ToInt16(data, 2) / 16384.0);
			float num3 = (float)((double)(-BitConverter.ToInt16(data, 4)) / 16384.0);
			float num4 = (float)((double)BitConverter.ToInt16(data, 6) / 16384.0);
			return new Quaternion(num, num2, num3, num4);
		}

		public Quaternion()
		{
			w = 1f;
			x = 0f;
			y = 0f;
			z = 0f;
		}

		public Quaternion(float x, float y, float z, float w)
		{
			this.w = w;
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static Quaternion operator *(Quaternion a, Quaternion b)
		{
			float num = a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z;
			float num2 = a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y;
			float num3 = a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x;
			float num4 = a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w;
			return new Quaternion(num2, num3, num4, num);
		}

		public void normalize()
		{
			float num = (float)(1.0 / System.Math.Sqrt(x * x + y * y + z * z + w * w));
			x *= num;
			y *= num;
			z *= num;
			w *= num;
		}

		public Vector3P transform(Vector3P xyz)
		{
			Quaternion quaternion = new Quaternion(xyz.X, xyz.Y, xyz.Z, 0f);
			Quaternion quaternion2 = this * quaternion * Inverse;
			return new Vector3P(quaternion2.x, quaternion2.y, quaternion2.z);
		}

		public float[,] asRotationMatrix()
		{
			float[,] array = new float[3, 3];
			double num = x * y;
			double num2 = w * z;
			double num3 = w * x;
			double num4 = w * y;
			double num5 = x * z;
			double num6 = y * z;
			double num7 = z * z;
			double num8 = y * y;
			double num9 = x * x;
			array[0, 0] = (float)(1.0 - 2.0 * (num8 + num7));
			array[0, 1] = (float)(2.0 * (num - num2));
			array[0, 2] = (float)(2.0 * (num5 + num4));
			array[1, 0] = (float)(2.0 * (num + num2));
			array[1, 1] = (float)(1.0 - 2.0 * (num9 + num7));
			array[1, 2] = (float)(2.0 * (num6 - num3));
			array[2, 0] = (float)(2.0 * (num5 - num4));
			array[2, 1] = (float)(2.0 * (num6 + num3));
			array[2, 2] = (float)(1.0 - 2.0 * (num9 + num8));
			return array;
		}
	}
	public class Vector3P : IDeserializable
	{
		private float[] xyz;

		public float X
		{
			get
			{
				return xyz[0];
			}
			set
			{
				xyz[0] = value;
			}
		}

		public float Y
		{
			get
			{
				return xyz[1];
			}
			set
			{
				xyz[1] = value;
			}
		}

		public float Z
		{
			get
			{
				return xyz[2];
			}
			set
			{
				xyz[2] = value;
			}
		}

		public double Length => System.Math.Sqrt(X * X + Y * Y + Z * Z);

		public float this[int i]
		{
			get
			{
				return xyz[i];
			}
			set
			{
				xyz[i] = value;
			}
		}

		public Vector3P()
			: this(0f)
		{
		}

		public Vector3P(float val)
			: this(val, val, val)
		{
		}

		public Vector3P(BinaryReaderEx input)
			: this(input.ReadSingle(), input.ReadSingle(), input.ReadSingle())
		{
		}

		public Vector3P(float x, float y, float z)
		{
			xyz = new float[3] { x, y, z };
		}

		public static Vector3P operator -(Vector3P a)
		{
			return new Vector3P(0f - a.X, 0f - a.Y, 0f - a.Z);
		}

		public void readCompressed(BinaryReaderEx input)
		{
			int num = input.ReadInt32();
			int num2 = num & 0x3FF;
			int num3 = (num >> 10) & 0x3FF;
			int num4 = (num >> 20) & 0x3FF;
			if (num2 > 511)
			{
				num2 -= 1024;
			}
			if (num3 > 511)
			{
				num3 -= 1024;
			}
			if (num4 > 511)
			{
				num4 -= 1024;
			}
			X = (float)((double)num2 * (-1.0 / 511.0));
			Y = (float)((double)num3 * (-1.0 / 511.0));
			Z = (float)((double)num4 * (-1.0 / 511.0));
		}

		public void write(BisDll.Stream.BinaryWriter output)
		{
			output.Write(X);
			output.Write(Y);
			output.Write(Z);
		}

		public static Vector3P operator *(Vector3P a, float b)
		{
			return new Vector3P(a.X * b, a.Y * b, a.Z * b);
		}

		public static float operator *(Vector3P a, Vector3P b)
		{
			return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
		}

		public static Vector3P operator +(Vector3P a, Vector3P b)
		{
			return new Vector3P(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		public static Vector3P operator -(Vector3P a, Vector3P b)
		{
			return new Vector3P(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Vector3P other))
			{
				return false;
			}
			if (base.Equals(obj))
			{
				return Equals(other);
			}
			return false;
		}

		public bool Equals(Vector3P other)
		{
			Func<float, float, bool> func = (float f1, float f2) => (double)System.Math.Abs(f1 - f2) < 0.05;
			if (func(X, other.X) && func(Y, other.Y))
			{
				return func(Z, other.Z);
			}
			return false;
		}

		public override string ToString()
		{
			CultureInfo cultureInfo = new CultureInfo("en-GB");
			return "{" + X.ToString(cultureInfo.NumberFormat) + "," + Y.ToString(cultureInfo.NumberFormat) + "," + Z.ToString(cultureInfo.NumberFormat) + "}";
		}

		public void ReadObject(BinaryReaderEx input)
		{
			xyz[0] = input.ReadSingle();
			xyz[1] = input.ReadSingle();
			xyz[2] = input.ReadSingle();
		}

		public float Distance(Vector3P v)
		{
			Vector3P vector3P = this - v;
			return (float)System.Math.Sqrt(vector3P.X * vector3P.X + vector3P.Y * vector3P.Y + vector3P.Z * vector3P.Z);
		}

		public void Normalize()
		{
			float num = (float)Length;
			X /= num;
			Y /= num;
			Z /= num;
		}
	}
	public class Vector3PCompressed : IDeserializable
	{
		private int value;

		private const float scaleFactor = -0.0019569471f;

		public float X
		{
			get
			{
				int num = value & 0x3FF;
				if (num > 511)
				{
					num -= 1024;
				}
				return (float)num * -0.0019569471f;
			}
		}

		public float Y
		{
			get
			{
				int num = (value >> 10) & 0x3FF;
				if (num > 511)
				{
					num -= 1024;
				}
				return (float)num * -0.0019569471f;
			}
		}

		public float Z
		{
			get
			{
				int num = (value >> 20) & 0x3FF;
				if (num > 511)
				{
					num -= 1024;
				}
				return (float)num * -0.0019569471f;
			}
		}

		public static implicit operator Vector3P(Vector3PCompressed src)
		{
			int num = src.value & 0x3FF;
			int num2 = (src.value >> 10) & 0x3FF;
			int num3 = (src.value >> 20) & 0x3FF;
			if (num > 511)
			{
				num -= 1024;
			}
			if (num2 > 511)
			{
				num2 -= 1024;
			}
			if (num3 > 511)
			{
				num3 -= 1024;
			}
			return new Vector3P((float)num * -0.0019569471f, (float)num2 * -0.0019569471f, (float)num3 * -0.0019569471f);
		}

		public static implicit operator int(Vector3PCompressed src)
		{
			return src.value;
		}

		public static implicit operator Vector3PCompressed(int src)
		{
			return new Vector3PCompressed
			{
				value = src
			};
		}

		public void ReadObject(BinaryReaderEx input)
		{
			value = input.ReadInt32();
		}
	}
}
