using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BoidsSystem : MonoBehaviour
{
    private List<Boid> _boids = new List<Boid>();

    public BoidInformation BoidInformation = new BoidInformation();

    private List<Transform> _landablePositions;
    public List<Transform> LandablePositions { get => _landablePositions; }
    private Dictionary<string, bool> _openSpots = new Dictionary<string, bool>();

    public Transform CenterpointPosition;

    void Awake()
    {
        _landablePositions = GameObject.FindGameObjectsWithTag("landable").Select(go => go.transform).ToList();
        _landablePositions.ForEach(lp => _openSpots.Add(lp.name, true));
        CenterpointPosition = GameObject.Find("BoidCenterPoint").transform;
    }

    void Start()
    {
        for (int _ = 0; _ < 17; _++)
        {
            SpawnBoid();
        }
    }

    void Update()
    {
        for (int i = 0; i < _boids.Count; i++)
        {
            BoidInformation.SwarmIndex = i;
            _boids[i].Simulate(_boids, BoidInformation);
        }
    }

    // Creates a new Boid gameobject and adds it to our list of Boids
    private void SpawnBoid()
    {
        var newBoid = Instantiate(PrefabManager.Instance.BoidPrefab, Vector3.zero, Quaternion.identity);
        newBoid.transform.position = _landablePositions[_boids.Count].position;
        _boids.Add(newBoid.GetComponent<Boid>());
        newBoid.GetComponent<Boid>().posName = _landablePositions[_boids.Count].name;
    }

    // Landing postions help methods
    public bool IsPositionBusy(string name)
    {
        if (_openSpots.ContainsKey(name))
        {
            if (_openSpots[name] == false)
            {
                return false;
            }
        }
        return true;
    }
    public void Occupy(string name) => _openSpots[name] = true;
    public void Vacant(string name) => _openSpots[name] = false;
}
