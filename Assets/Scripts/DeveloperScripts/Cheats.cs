using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cheats : MonoBehaviour
{
    private Transform _player;

    private Vector2 _startPos;

    private void Start()
    {
        _player = GameObject.FindWithTag("Player").transform;

        _startPos = _player.position;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            _player.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            _player.position = _startPos;
        }
    }
}
