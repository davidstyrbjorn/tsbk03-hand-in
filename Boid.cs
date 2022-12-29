using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct BoidInformation
{
    public int SwarmIndex { get; set; }
    public float SeperationRadius;
    public float LocalAreaRadius;
    public float Speed;
    public float SteeringSpeed;
    public float PlayerNearThreshold;
    [Range(0f, 1f)]
    public float AlignmentWeight;
    [Range(0f, 1f)]
    public float SeperationWeight;
    [Range(0f, 1f)]
    public float CohesionWeight;
}

public enum BoidState
{
    Flying,
    Takeoff,
    Landing,
    Landed
}

public struct BoidMeta
{
    public Vector3 ScaredOrigin;
    public Vector3 ScaredOffInDir;
    public Vector3 LandingDir;
    public Transform RecordedLandingPosition;
}

public class Boid : MonoBehaviour
{
    private BoidState _state;
    public BoidState State { get => _state; }
    private BoidMeta _meta;
    private BoidsSystem _boidSystem;
    public string posName;

    private float timeUntillLanding = 10.0f;
    public static float LandThreshold = 7.0f;
    private float _distanceToLand = 0.0f;
    private const float _centerMax = 25.0f;

    private void Start()
    {
        _meta = new BoidMeta();
        _state = BoidState.Landed;
        _boidSystem = FindObjectOfType<BoidsSystem>();
    }

    public void Simulate(List<Boid> boids, BoidInformation boidInformation)
    {
        StateReducer(boids, boidInformation);
    }

    private void Fly(List<Boid> boids, BoidInformation boidInformation)
    {
        // Check if we want to land
        if (timeUntillLanding <= 0.0f)
        {
            var (shouldLand, positionToLand) = InVicinityOfLandablePosition();
            if (shouldLand)
            {
                _state = BoidState.Landing;
                _meta.RecordedLandingPosition = positionToLand;
                _distanceToLand = Vector3.Distance(_meta.RecordedLandingPosition.position, transform.position);
            }
        }

        float dt = Time.deltaTime;

        //decrease time untill allowed to land again
        timeUntillLanding -= dt;

        //default vars
        var steering = Vector3.zero;

        Vector3 separationDirection = Vector3.zero;
        Vector3 alignmentDirection = Vector3.zero;
        Vector3 cohesionDirection = Vector3.zero;
        Vector3 centerpointDirection = Vector3.zero;

        int alignmentCount = 0;
        int separationCount = 0;
        int cohesionCount = 0;

        foreach (Boid boid in boids)
        {
            //skip self
            if (boid == this)
                continue;

            //boids in landed position dont have an impact on the murder.
            if (boid.State == BoidState.Landed)
                continue;

            var distance = Vector3.Distance(boid.transform.position, this.transform.position);

            //identify local neighbour
            if (distance < boidInformation.SeperationRadius)
            {
                separationDirection += boid.transform.position - transform.position;
                separationCount++;
            }

            if (distance < boidInformation.LocalAreaRadius)
            {
                alignmentDirection += boid.transform.forward;
                alignmentCount++;
            }
            if (distance < boidInformation.LocalAreaRadius)
            {
                cohesionDirection += boid.transform.position - transform.position;
                cohesionCount++;
            }
        }

        //move towards centerpoint
        centerpointDirection = FindObjectOfType<BoidsSystem>().CenterpointPosition.position - transform.position;

        float t = Mathf.Min(centerpointDirection.magnitude / _centerMax, 1.0f);
        float centerpointWeight = InQuint(t);

        //calculate average
        if (separationCount > 0)
            separationDirection /= separationCount;
        if (alignmentCount > 0)
            alignmentDirection /= alignmentCount;
        if (cohesionCount > 0)
            cohesionDirection /= cohesionCount;

        //flip and normalize
        separationDirection = boidInformation.SeperationWeight * -separationDirection.normalized;
        alignmentDirection = boidInformation.AlignmentWeight * alignmentDirection.normalized;
        cohesionDirection = boidInformation.CohesionWeight * cohesionDirection.normalized;
        centerpointDirection = centerpointWeight * centerpointDirection.normalized;

        //apply to steering
        steering += separationDirection;
        steering += alignmentDirection;
        steering += cohesionDirection;
        steering += centerpointDirection;

        var layer =
                 (1 << LayerMask.NameToLayer("OuterPerimeter"))
               | (1 << LayerMask.NameToLayer("NonColisionPlatform"))
               | (1 << LayerMask.NameToLayer("BoidAvoid"));
        RaycastHit hitInfo;
        if (Physics.Raycast(transform.position, transform.forward, out hitInfo, 0.8f, layer))
        {
            steering = -(hitInfo.point - transform.position).normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(steering), 1000000000);
        }

        //apply steering
        if (steering != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(steering), boidInformation.SteeringSpeed * dt);

        //move 
        transform.position += transform.TransformDirection(new Vector3(0, 0, boidInformation.Speed)) * dt;
    }

    private void Landed(BoidInformation boidInformation)
    {
        Quaternion rot = Quaternion.Slerp(transform.rotation, Quaternion.identity, 5.0f * Time.deltaTime);
        transform.rotation = rot;
        transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        //players.Add(playerInput.GetComponent<Player>());
        //var players = PlayerManager.Instance.players;

        foreach (var player in players)
        {
            if (Vector3.Distance(player.transform.position, transform.position) < boidInformation.PlayerNearThreshold)
            {
                // Update state and some meta data
                _state = BoidState.Takeoff;
                var dir = (transform.position - player.transform.position);
                dir += AddNoiseOnAngle(-50, 50);

                _boidSystem.Vacant(_meta.RecordedLandingPosition.name);
                _meta.ScaredOffInDir = dir;
                _meta.ScaredOrigin = transform.position;
            }
        }
    }

    private void Takeoff(BoidInformation boidInformation)
    {
        _boidSystem.Vacant(posName);
        float takeOffMultiplier = 5.0f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_meta.ScaredOffInDir), boidInformation.SteeringSpeed * Time.deltaTime * takeOffMultiplier);
        transform.position += transform.TransformDirection(new Vector3(0, 0, boidInformation.Speed * takeOffMultiplier)) * Time.deltaTime;
        if (Vector3.Distance(transform.position, _meta.ScaredOrigin) > 5)
        {
            _state = BoidState.Flying;
            timeUntillLanding = 10.0f;
        }
    }

    private void Landing(BoidInformation boidInformation)
    {
        float landingMultiplier = 5.0f;

        // We know we are within LandingThreshold distnace to _meta.RecordedLandingPosition
        float t = 1.0f - (Vector3.Distance(transform.position, _meta.RecordedLandingPosition.position) / _distanceToLand);
        //rounding errors cause it to get stuck on 0 so we need to make sure it starts at a value so the bird moves initally
        t = Mathf.Clamp(t, 0.01f, 1.0f);
        float interpolatedValue = EaseOutCubic(t);

        _meta.LandingDir = (_meta.RecordedLandingPosition.position - transform.position).normalized;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_meta.LandingDir), boidInformation.SteeringSpeed * Time.deltaTime * landingMultiplier * interpolatedValue * 1000.0f);
        transform.position += transform.TransformDirection(new Vector3(0, 0, boidInformation.Speed * landingMultiplier * interpolatedValue)) * Time.deltaTime;

        if (Vector3.Distance(transform.position, _meta.RecordedLandingPosition.position) < 0.01f)
        {
            _state = BoidState.Landed;
        }
    }

    void StateReducer(List<Boid> boids, BoidInformation boidInformation)
    {
        switch (_state)
        {
            case BoidState.Flying:
                Fly(boids, boidInformation);
                break;
            case BoidState.Landed:
                Landed(boidInformation);
                break;
            case BoidState.Landing:
                Landing(boidInformation);
                break;
            case BoidState.Takeoff:
                Takeoff(boidInformation);
                break;
        }
    }

    private (bool, Transform) InVicinityOfLandablePosition()
    {
        var positions = FindObjectOfType<BoidsSystem>().LandablePositions;

        foreach (var pos in positions)
        {
            // Check distnace to a certain point above our landable position
            if (Vector3.Distance(transform.position, pos.position + Vector3.up * LandThreshold) < LandThreshold
                && !_boidSystem.IsPositionBusy(pos.name))
            {
                _boidSystem.Occupy(pos.name);
                posName = pos.name;
                return (true, pos);
            }
        }

        return (false, null);
    }

    private Vector3 AddNoiseOnAngle(float min, float max)
    {
        // Find random angle between min & max inclusive
        float xNoise = Random.Range(min, max);
        float yNoise = Random.Range(min, max);
        float zNoise = Random.Range(min, max);

        // Convert Angle to Vector3
        Vector3 noise = new Vector3(
          Mathf.Sin(2 * Mathf.PI * xNoise / 360),
          Mathf.Sin(2 * Mathf.PI * yNoise / 360),
          Mathf.Sin(2 * Mathf.PI * zNoise / 360)
        );
        return noise;
    }

    private float EaseOutCubic(float t)
    {
        return Mathf.Sin(t * Mathf.PI) / 2;
    }
    public static float InQuint(float t) => t * t * t * t * t;
}
