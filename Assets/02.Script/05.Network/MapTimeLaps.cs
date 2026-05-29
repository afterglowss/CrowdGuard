using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapTimeLaps : MonoBehaviour
{
    public List<Renderer> renderers;
    public float delay;
    [ContextMenu("SortTransfoms")]
    public void SortTransforms()
    {
        renderers = GetComponentsInChildren<Renderer>().ToList();
        renderers.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

    }

    public void Start()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (!renderers[i]) continue;
            Debug.Log($" { i} : {renderers[i].name}");
            renderers[i].enabled = false;
        }

        StartCoroutine(TimeLaps());
    }

    IEnumerator TimeLaps()
    {
        foreach (var renderer in renderers)
        {
            if (!renderer) continue;
            renderer.enabled = true;
            yield return new WaitForSeconds(delay);
        }
    }
}
