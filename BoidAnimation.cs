using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidAnimation : MonoBehaviour
{
    private Animator _animator;
    private Boid _boid;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _boid = GetComponent<Boid>();
        _animator.SetFloat("speedModifier", Random.Range(0.5f, 1.0f));
    }

    void LateUpdate()
    {
        if (_boid.State == BoidState.Takeoff || _boid.State == BoidState.Flying || _boid.State == BoidState.Landing)
        {
            _animator.SetBool("flying", true);
            _animator.SetBool("landed", false);
        }
        else
        {
            _animator.SetBool("flying", false);
            _animator.SetBool("landed", true);
        }
    }
}
