using System.Collections.Generic;
using UnityEngine;

public class BulletManager : MonoBehaviour
{
    public static BulletManager Instance { get; private set; }

    private List<GameObject> bullets = new List<GameObject>();
    private List<GameObject> impactMarkers = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterBullet(GameObject bullet)
    {
        bullets.Add(bullet);
    }

    public void RegisterImpactMarker(GameObject marker)
    {
        impactMarkers.Add(marker);
    }

    public void ClearAllBullets()
    {
        foreach (GameObject bullet in bullets)
        {
            if (bullet != null)
            {
                Destroy(bullet);
            }
        }

        foreach (GameObject marker in impactMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        bullets.Clear();
        impactMarkers.Clear();
    }

    public int GetBulletCount()
    {
        return bullets.Count;
    }

    public int GetMarkerCount()
    {
        return impactMarkers.Count;
    }
}