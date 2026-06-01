using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class MapTimeLaps : MonoBehaviour
{
    public Camera cam;
    public List<Renderer> renderers;
    public float time;
    public Vector3 startPos;
    public Vector3 endPos;
    public AnimationCurve curve;
    public bool isOnce;

    public List<GameObject> objects;
    [ContextMenu("SortTransfoms")]
    public void SortTransforms()
    {
        renderers = GetComponentsInChildren<Renderer>().ToList();
        renderers.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

    }

    [ContextMenu("CleanTransforms")]
    public void CleanTransforms()
    {
        List<Renderer> renderersToRemove = new List<Renderer>();
        foreach (var r in renderers)
        {
            if (r) continue;
            renderersToRemove.Add(r);
        }

        foreach (var r in renderersToRemove)
        {
            renderers.Remove(r);
        }
    }

    public void Start()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (!renderers[i]) continue;
            Debug.Log($" { i} : {renderers[i].name}");
            renderers[i].transform.position -= Vector3.up*30;
            renderers[i].enabled = false;
            
        }
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isOnce)
            {
                isOnce = true;
                StartCoroutine(TimeLaps());
                StartCoroutine(CameraMove());
            }
            
        }
    }

    IEnumerator TimeLaps()
    {
        float delay = time / (renderers.Count - 1);
        float t = 0;
        float progress = 0;
        cam.transform.position = startPos;
        foreach (var renderer in renderers)
        {
            if (!renderer) continue;
            renderer.enabled = true;
            renderer.transform.DOMove(renderer.transform.position + Vector3.up * 30, 1f);
            
            
            
            if (renderers.Last() == renderer) break;
            yield return new WaitForSeconds(delay);
            t += delay;
        }
    }

    IEnumerator CameraMove()
    {
        cam.transform.position = startPos;
        yield return null;
        cam.transform.DOMove(endPos, time + 3f).SetEase(curve);
        
    }
}
