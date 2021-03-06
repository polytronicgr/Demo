using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace OpenTkConsole
{
	class Mesh
	{
		public int VAOHandle;
		public int BufferHandle;
        public int IndexBufferHandle;
		public int VertexAmount;

		private float rotationY;
		
		public struct PosNorTexVertex
		{
			Vector3 position;
			Vector3 normal;
			Vector2 texCoord;

			public PosNorTexVertex(Vector3 pos, Vector3 nor, Vector2 uv)
			{
				position = pos;
				normal = nor;
				texCoord = uv;
			}

            

			const int bytesPerFloat = 4;

			public static int getPositionSizeBytes()
			{
				return getElementsInPosition() * bytesPerFloat;
			}

			public static int getTexCoordSizeBytes()
			{
				return getElementsInTexCoord() * bytesPerFloat;
			}
			
			public static int getNormalSizeBytes() { return getElementsInNormal() * bytesPerFloat; }

			public static int getElementsInPosition()
			{
				return 3;
			}
			
			public static int getElementsInNormal() { return getElementsInPosition(); }

			public static int getElementsInTexCoord()
			{
				return 2;
			}
		}

		private List<PosNorTexVertex> rawVertices;
        private List<uint> rawIndices;
		
		public Matrix4Uniform worldMatrix;

		public static int PositionDataIndex { get; set; }
		public static int NormalDataIndex { get; set; }
		public static int TexCoordDataIndex { get; set; }
		public static int ColorDataIndex { get; set; }
		public static int ScaleDataIndex { get; set; }

		// RenderingComponent
		public Color4 DiffuseColor { get; set; }

		public float Scale { get; set; }
		//

	
		public MaterialManager.Material MeshMaterial { get; set; }

		// TransformComponent
		private Vector3 worldPosition;
		
		public Vector3 WorldPosition 
		{
			get
			{
				return worldPosition;
			}
			set
			{
				worldPosition = value;
				worldMatrix.Matrix = Matrix4.CreateTranslation(worldPosition);
			}	
		}

		//

		static public Mesh CreateTriangleMesh()
		{
			// positions

			List<PosNorTexVertex> vertices = new List<PosNorTexVertex>(3);
			Vector3 normal = new Vector3(0.0f, 0.0f, 1.0f);
			vertices.Add(new PosNorTexVertex(new Vector3(-1f, 1f, 0.0f), normal, new Vector2(0.0f,1.0f)));
			vertices.Add(new PosNorTexVertex(new Vector3(1f, 1f, 0.0f), normal, new Vector2(1.0f, 1.0f)));
			vertices.Add(new PosNorTexVertex(new Vector3(0.0f, 0.0f, 0.0f), normal, new Vector2(0.5f, 0.0f)));

			return new Mesh(vertices, MaterialManager.getMaterialByName("white"));
		}
			
		public Mesh(List<PosNorTexVertex> vertices, MaterialManager.Material meshMaterial)
		{
			BufferHandle = GL.GenBuffer();
			VAOHandle = GL.GenVertexArray();
            IndexBufferHandle = GL.GenBuffer();
			
			VertexAmount = vertices.Count;
			
			Error.checkGLError("Mesh constructor");

			 rawVertices = vertices;

			MeshMaterial = meshMaterial;
			 
			 // Transformcomponent
			 worldMatrix = new Matrix4Uniform("modelMatrix");
			 worldMatrix.Matrix = Matrix4.Identity;

			// RenderingComponent
			Scale = 1.0f;
			DiffuseColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
			rotationY = 0.0f;

		}
		
		public void bufferData()
		{
			int vertexSize = PosNorTexVertex.getPositionSizeBytes() + PosNorTexVertex.getNormalSizeBytes() + PosNorTexVertex.getTexCoordSizeBytes();
       

			GL.BindVertexArray(VAOHandle);
			
			GL.BindBuffer(BufferTarget.ArrayBuffer, BufferHandle);
			
			GL.BufferData(BufferTarget.ArrayBuffer, VertexAmount * vertexSize, rawVertices.ToArray(), BufferUsageHint.StaticDraw);

            //  Vertex attributes

            GL.VertexAttribPointer(index: PositionDataIndex, size: PosNorTexVertex.getElementsInPosition()
                , type: VertexAttribPointerType.Float
                , normalized: false, stride: vertexSize, offset: 0);

		   
		   GL.VertexAttribPointer(index: NormalDataIndex, size: PosNorTexVertex.getElementsInNormal()
		   , type: VertexAttribPointerType.Float
		   , normalized: false, stride: vertexSize, offset: PosNorTexVertex.getPositionSizeBytes());
		   
		   
			GL.VertexAttribPointer(index: TexCoordDataIndex, size: PosNorTexVertex.getElementsInTexCoord()
		   , type: VertexAttribPointerType.Float
		   , normalized: false, stride: vertexSize, offset: PosNorTexVertex.getNormalSizeBytes() + PosNorTexVertex.getNormalSizeBytes());

			GL.EnableVertexAttribArray(PositionDataIndex);
			GL.EnableVertexAttribArray(TexCoordDataIndex);
			
			Error.checkGLError("Mesh.bufferData");
		}

		public void updateUniforms(ShaderProgram shaderProgram)
		{
			worldMatrix.Set(shaderProgram);
			
			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, MeshMaterial.textureGLIndex);

			GL.Uniform4(ColorDataIndex, DiffuseColor);
			GL.Uniform1(ScaleDataIndex, Scale);

			Error.checkGLError("Mesh.updateUniforms");
		}

		public void rotate(float speed)
		{
			rotationY += speed;
			if (rotationY > MathHelper.TwoPi)
			{
				rotationY = 0.0f;
			}
			worldMatrix.Matrix = Matrix4.CreateRotationY(rotationY);
		}

        // Reads on .obs file
        static public Mesh CreateFromFile(string filename)
        {
            List<OBJFileReader.OBJFace> faces = new List<OBJFileReader.OBJFace>();

            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> texCoords = new List<Vector2>();
            MaterialManager.Material meshMaterial = null;

            OBJFileReader.readOBJ(filename, ref faces, ref positions, ref normals, ref texCoords, ref meshMaterial);

            // Create positions 
            List<PosNorTexVertex> vertices = new List<PosNorTexVertex>(positions.Count);

            Console.WriteLine("Mesh read from " + filename);
			
            foreach (OBJFileReader.OBJFace face in faces)
            {
				vertices.Add( new PosNorTexVertex(positions[(int)face.positionIndex - 1],  normals[(int)face.normalIndex - 1], texCoords[(int)face.texCoordIndex - 1]));
            }
			
            return new Mesh(vertices, meshMaterial);
		}
	}
}