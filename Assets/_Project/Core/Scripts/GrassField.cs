using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GlossyHorizons.Core
{
    /// <summary>
    /// Dibuja pasto con GPU instancing sobre la malla del terreno.
    /// No crea GameObjects ni guarda datos por brizna: las posiciones se generan con una semilla
    /// al activarse, así que la escena y el repositorio quedan livianos.
    /// </summary>
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public string name = "Hoja";
            public Mesh mesh;
            public Material[] materials;
            [Tooltip("Instancias por metro cuadrado.")]
            public float density = 4f;
            public Vector2 scaleRange = new Vector2(0.8f, 1.3f);
            public bool castShadows;
        }

        [Header("Terreno")]
        public MeshFilter ground;

        [Header("Capas de pasto")]
        public Layer[] layers = Array.Empty<Layer>();

        [Header("Zonas sin pasto")]
        [Tooltip("Centro de la cúpula (solo se usa X y Z).")]
        public Vector3 domeCenter;
        [Tooltip("No se dibuja pasto dentro de este radio (el interior de la cúpula).")]
        public float innerRadius = 16.3f;
        [Tooltip("Cajas en espacio de mundo donde no va pasto (caminos, salidas).")]
        public Bounds[] exclusions = Array.Empty<Bounds>();

        [Header("Generación")]
        public int seed = 1234;
        [Tooltip("Tamaño de cada bloque; Unity descarta los bloques fuera de la cámara.")]
        public float chunkSize = 10f;

        const int MaxPerCall = 1023;

        class Chunk
        {
            public Bounds bounds;
            public List<Matrix4x4>[] perLayer;
            public Matrix4x4[][] arrays;
        }

        readonly List<Chunk> chunks = new List<Chunk>();
        int instanceCount;
        public int InstanceCount => instanceCount;

        void OnEnable() => Generate();
        void OnValidate() { if (isActiveAndEnabled) Generate(); }

        [ContextMenu("Regenerar pasto")]
        public void Generate()
        {
            chunks.Clear();
            instanceCount = 0;
            if (ground == null || ground.sharedMesh == null || layers == null || layers.Length == 0) return;

            var mesh = ground.sharedMesh;
            if (!mesh.isReadable)
            {
                Debug.LogWarning($"[GrassField] La malla {mesh.name} necesita Read/Write activado.", this);
                return;
            }

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var l2w = ground.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++) verts[i] = l2w.MultiplyPoint3x4(verts[i]);

            var rng = new System.Random(seed);
            var map = new Dictionary<Vector2Int, Chunk>();
            float r2 = innerRadius * innerRadius;

            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (area <= 0f) continue;

                for (int li = 0; li < layers.Length; li++)
                {
                    var layer = layers[li];
                    if (layer == null || layer.mesh == null || layer.density <= 0f) continue;

                    float expected = area * layer.density;
                    int count = (int)expected + (rng.NextDouble() < expected - (int)expected ? 1 : 0);
                    for (int k = 0; k < count; k++)
                    {
                        float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                        Vector3 p = a + (b - a) * u + (c - a) * v;
                        float yaw = (float)rng.NextDouble() * 360f;
                        float s = Mathf.Lerp(layer.scaleRange.x, layer.scaleRange.y, (float)rng.NextDouble());

                        float dx = p.x - domeCenter.x, dz = p.z - domeCenter.z;
                        if (dx * dx + dz * dz < r2 || Excluded(p)) continue;

                        var key = new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));
                        if (!map.TryGetValue(key, out var chunk))
                        {
                            chunk = new Chunk { bounds = new Bounds(p, Vector3.zero), perLayer = new List<Matrix4x4>[layers.Length] };
                            for (int j = 0; j < layers.Length; j++) chunk.perLayer[j] = new List<Matrix4x4>();
                            map[key] = chunk;
                        }
                        chunk.perLayer[li].Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * s));
                        chunk.bounds.Encapsulate(p);
                        instanceCount++;
                    }
                }
            }

            foreach (var chunk in map.Values)
            {
                chunk.bounds.Expand(new Vector3(1f, 2f, 1f)); // margen para la altura de las briznas
                chunk.arrays = new Matrix4x4[layers.Length][];
                for (int j = 0; j < layers.Length; j++) chunk.arrays[j] = chunk.perLayer[j].ToArray();
                chunk.perLayer = null;
                chunks.Add(chunk);
            }
        }

        bool Excluded(Vector3 p)
        {
            foreach (var e in exclusions)
                if (p.x >= e.min.x && p.x <= e.max.x && p.z >= e.min.z && p.z <= e.max.z) return true;
            return false;
        }

        void Update()
        {
            for (int li = 0; li < layers.Length; li++)
            {
                var layer = layers[li];
                if (layer == null || layer.mesh == null || layer.materials == null) continue;

                int subCount = Mathf.Min(layer.mesh.subMeshCount, layer.materials.Length);
                for (int sub = 0; sub < subCount; sub++)
                {
                    var mat = layer.materials[sub];
                    if (mat == null) continue;
                    var rp = new RenderParams(mat)
                    {
                        layer = gameObject.layer,
                        shadowCastingMode = layer.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        receiveShadows = true,
                    };
                    foreach (var chunk in chunks)
                    {
                        var arr = chunk.arrays[li];
                        if (arr.Length == 0) continue;
                        rp.worldBounds = chunk.bounds; // Unity descarta el bloque si no está en cámara
                        for (int start = 0; start < arr.Length; start += MaxPerCall)
                            Graphics.RenderMeshInstanced(rp, layer.mesh, sub, arr, Mathf.Min(MaxPerCall, arr.Length - start), start);
                    }
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.6f);
            foreach (var chunk in chunks) Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
        }
    }
}
