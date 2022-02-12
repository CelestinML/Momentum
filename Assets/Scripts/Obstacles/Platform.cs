using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform : MonoBehaviour
{
    private Transform _playerBottom;

    private Collider2D _collider;

    private void Start()
    {
        _playerBottom = GameObject.FindWithTag("PlayerBottom").transform;

        _collider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (_playerBottom.position.y <= transform.position.y)
        {
            _collider.enabled = false;
        }
        else
        {
            _collider.enabled = true;
        }
    }
}
