using UnityEngine;

// Used only when the real 3D loom (Resources/KenteGame/kente_strip_loom.glb) can't load, e.g. glTFast isn't installed.
// Same part names and sizes as the real model, so the weaving game still plays.
public static class KenteStandInLoom
{
    public static GameObject Build()
    {
        var root = new GameObject("KenteStandInLoom");
        Color wood = new Color32(192, 122, 68, 255), dark = new Color32(138, 79, 42, 255), light = new Color32(216, 147, 90, 255);
        Part(root.transform, "frame_post_L", PrimitiveType.Cube, new Vector3(-2.75f, 2f, -.3f), new Vector3(.36f, 6.2f, .36f), wood, "wood");
        Part(root.transform, "frame_post_R", PrimitiveType.Cube, new Vector3(2.75f, 2f, -.3f), new Vector3(.36f, 6.2f, .36f), wood, "wood");
        Part(root.transform, "frame_back_L", PrimitiveType.Cube, new Vector3(-2.75f, 2.2f, -1.4f), new Vector3(.34f, 6.6f, .34f), dark, "wood_dark");
        Part(root.transform, "frame_back_R", PrimitiveType.Cube, new Vector3(2.75f, 2.2f, -1.4f), new Vector3(.34f, 6.6f, .34f), dark, "wood_dark");
        Part(root.transform, "frame_top_beam", PrimitiveType.Cube, new Vector3(0f, 4.72f, -.05f), new Vector3(5.9f, .3f, .3f), wood, "wood");
        Part(root.transform, "frame_front_beam", PrimitiveType.Cube, new Vector3(0f, -.32f, .1f), new Vector3(5.9f, .3f, .3f), wood, "wood");
        var cloth = Part(root.transform, "cloth", PrimitiveType.Quad, new Vector3(0f, 2.15f, 0f), new Vector3(4f, 4.3f, 1f), Color.white, "cloth");
        cloth.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the viewer (+Z)
        var batten = new GameObject("batten").transform; batten.SetParent(root.transform, false); batten.localPosition = new Vector3(0f, .6f, .3f);
        Part(batten, "batten_bar", PrimitiveType.Cube, Vector3.zero, new Vector3(4.9f, .2f, .26f), wood, "wood");
        var shuttle = new GameObject("shuttle").transform; shuttle.SetParent(root.transform, false); shuttle.localPosition = new Vector3(0f, .22f, .25f);
        Part(shuttle, "shuttle_body", PrimitiveType.Cube, Vector3.zero, new Vector3(1.3f, .3f, .26f), light, "wood_light");
        var bob = Part(shuttle, "bobbin", PrimitiveType.Cylinder, new Vector3(0f, 0f, .18f), new Vector3(.2f, .31f, .2f), new Color32(216, 199, 173, 255), "bobbin_mat");
        bob.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        return root;
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, string matName)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col); // never block pin taps
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        var sh = Shader.Find("Sprites/Default");
        var m = new Material(sh != null ? sh : Shader.Find("Unlit/Color")) { name = matName };
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }
}
