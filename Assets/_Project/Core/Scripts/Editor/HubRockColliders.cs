using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>Colisión precisa en las rocas del Hub: collider convexo con la forma real del modelo.
    /// Las esferas originales (ocultas, ya reemplazadas por modelos) pierden su collider esférico, que era más grande.</summary>
    public static class HubRockColliders
    {
        [MenuItem("Glossy Horizons/Hub/Colisiones en rocas")]
        static void Run()
        {
            int modelos = 0, esferas = 0, quitadas = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                var go = mf.gameObject;
                if (mf.sharedMesh == null) continue;
                var mr = go.GetComponent<MeshRenderer>();
                bool modeloRoca = AssetDatabase.GetAssetPath(mf.sharedMesh).Contains("/Roca_");
                if (modeloRoca)
                {
                    foreach (var c0 in go.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c0);
                    var c = Undo.AddComponent<MeshCollider>(go);
                    c.sharedMesh = mf.sharedMesh; c.convex = true;
                    modelos++;
                }
                else if (go.name.StartsWith("Roca") && mf.sharedMesh.name.Contains("Sphere"))
                {
                    if (mr != null && !mr.enabled)
                    {   // esfera oculta, reemplazada por un modelo con su propio collider
                        foreach (var c0 in go.GetComponents<Collider>()) { Undo.DestroyObjectImmediate(c0); quitadas++; }
                    }
                    else if (!go.TryGetComponent<Collider>(out _)) { Undo.AddComponent<SphereCollider>(go); esferas++; }
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Rocas: {modelos} modelos con collider convexo, {quitadas} colliders esféricos quitados, {esferas} esferas visibles con collider. Guarda la escena (Ctrl+S).");
        }
    }
}
