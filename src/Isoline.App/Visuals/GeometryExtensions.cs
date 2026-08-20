using System.Windows.Media.Media3D;
using Isoline.Util;

namespace Isoline.Visuals
{
	/// <summary>
	/// Bridges Isoline.Core's plain <see cref="Vector3"/> to WPF's 3D types.
	/// <para>
	/// These conversions used to live on Vector3 itself, which quietly made the whole
	/// G-code core depend on PresentationCore - and therefore on Windows. Keeping them
	/// here as extension methods is what lets the core build and its tests run anywhere.
	/// </para>
	/// </summary>
	public static class GeometryExtensions
	{
		public static Point3D ToPoint3D(this Vector3 v)
		{
			return new Point3D(v.X, v.Y, v.Z);
		}

		public static Vector3D ToVector3D(this Vector3 v)
		{
			return new Vector3D(v.X, v.Y, v.Z);
		}

		public static Point3D ToPoint3D(this Vector2 v, double z)
		{
			return new Point3D(v.X, v.Y, z);
		}

		public static Vector3 ToVector3(this Point3D p)
		{
			return new Vector3(p.X, p.Y, p.Z);
		}

		public static Vector3 ToVector3(this Vector3D v)
		{
			return new Vector3(v.X, v.Y, v.Z);
		}
	}
}
