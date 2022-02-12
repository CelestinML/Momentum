using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform : MonoBehaviour
{
    [SerializeField] private float topOffset = .05f;
    
    [SerializeField] private float bottomOffset = .5f;
    
    private Transform _playerWheel;

    private float _wheelRadius;

    private float _thresholdHeight;

    private Collider2D _collider;

    private void Start()
    {
        _playerWheel = GameObject.FindWithTag("Player").transform.Find("Body/Wheel");
        _wheelRadius = Vector2.Distance(_playerWheel.position, _playerWheel.Find("Player Bottom").position);

        _collider = GetComponent<Collider2D>();
        _collider.enabled = false;

        _thresholdHeight = transform.position.y + _wheelRadius;
    }

    private void Update()
    {
        if (_playerWheel.position.y >= _thresholdHeight + topOffset)
        {
            _collider.enabled = true;
        }
        else if (_playerWheel.position.y <= _thresholdHeight - bottomOffset)
        {
            _collider.enabled = false;
        }
    }
}
