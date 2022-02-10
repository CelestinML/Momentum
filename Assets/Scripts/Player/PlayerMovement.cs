using System;
using System.Collections.Generic;
using System.Numerics;
using DG.Tweening;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayerMovement : MonoBehaviour
{
    //Stored rigidbody
    private Rigidbody2D _rb;
    
    
    [Header("Jump parameters")] 
    
    //Serialized
    
    public float jumpForce = 20;

    [Range(0, 3)]
    public float bodyMaxInflate = 2;

    [Range(0, 1)]
    public float bodyMaxShift = .3f;
    
    public float jumpSequenceDuration = .3f;
    
    //Private
    
    //Inputs
    private bool _currentJumpInput = false;
    private bool _previousJumpInput = false;
    private bool _jumpInput = false;
    
    //To make sure we check the positive jump input in the next fixed update
    private bool _jumpInputHasBeenChecked = true;
    
    //To avoid jumping many times at at once
    private bool _allowJump = false;
    
    //Jump attributes
    private Sequence _jumpSequence = null;
    
    //Calculated according to the body's size
    private float _jumpCheckDistance;
    
    
    [Header("Move parameters")]
    
    //Serialized
    
    public float maxSpeed = 10;

    public float slowPerSecond = 1;

    public float accelerationPerSecond = 2;
    
    //Private
    
    //Stored inputs
    private float _horizontalInput = 0;
    
    //Calculated target speed (basically _horizontalInput * maxSpeed)
    private float _targetSpeed = 0;
    
    //Stores the last known ground normal
    private Vector2 _moveDirection;
    

    [Header("Other parameters")]
    
    public float maxWalkableAngle = 35;

    [Range(0, 1)] [SerializeField] private float minimumInput = 0.05f;
    
    [SerializeField] private float maxFallingSpeed = 25;
    

    [Header("Collision detection")] 
    
    //Serialized
    
    [Range(8, 360)] [SerializeField] private int raycastCount = 360;

    [SerializeField] private Transform groundCheck;
    
    //Calculated check distance
    private float _checkDistance;
    
    //Checks if the player is on the ground or not
    private bool _isGrounded = true;
    

    [Header("GFX")] 
    
    //Serialized
    
    [SerializeField] private Transform body;

    [SerializeField] private float flipThreshold = 1;
    
    //Private
    
    //Stored renderer
    private SpriteRenderer _renderer;
    
    //Store current angle to rotate player accordingly
    private float _currentAngle = 0;
    

    [Header("Particle System")] 
    
    [SerializeField] private ParticleSystem ps;

    //Defines where the particles will be emitted from
    [SerializeField] private Transform playerBottom;

    //Stores the particle system's emission system to be able to modify the emission rate
    private ParticleSystem.EmissionModule _psEmission;

    //Stores the original particle rate over time
    private float _particlesRateOverTime;
    

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _renderer = GetComponent<SpriteRenderer>();

        _checkDistance = Vector2.Distance(transform.position, groundCheck.position);

        _psEmission = ps.emission;
        _particlesRateOverTime = (int) _psEmission.rateOverTime.constant;

        _jumpCheckDistance = body.GetComponent<Renderer>().bounds.size.x / 2 * bodyMaxInflate + bodyMaxShift;

        _jumpSequence = SetupJumpSequence();
    }

    private void Update()
    {
        GetInputs();

        OrientatePlayer();

        HandleParticles();
    }
    
    private void FixedUpdate()
    {
        CheckGrounded();

        AdaptGravity();

        MovePlayer();

        _jumpInputHasBeenChecked = true;
    }

    private void GetInputs()
    {
        //Get move input
        _horizontalInput = Input.GetAxisRaw("Horizontal");

        if (_jumpInputHasBeenChecked)
        {
            //Get jump input
            _currentJumpInput = (Input.GetAxisRaw("Jump") > 0);

            _jumpInput = !_previousJumpInput && _currentJumpInput;

            _previousJumpInput = _currentJumpInput;

            _jumpInputHasBeenChecked = false;
        }
    }

    //GFX FUNCTIONS
    
    private void OrientatePlayer()
    {
        float yRotation = transform.rotation.y;

        float projectedSpeed = Vector2.Dot(_rb.velocity, _moveDirection);
        
        if (projectedSpeed < -flipThreshold)
        {
            yRotation = 180;
        }
        else if (projectedSpeed > flipThreshold)
        {
            yRotation = 0;
        }

        float zRotation;
        if (yRotation == 180)
        {
            zRotation = _currentAngle;
        }
        else
        {
            zRotation = -_currentAngle;
        }

        transform.rotation = Quaternion.Euler(0, yRotation, zRotation);
    }

    private void HandleParticles()
    {
        ps.transform.position = playerBottom.position;

        if (Mathf.Approximately(_horizontalInput, 0) || !_isGrounded)
        {
            _psEmission.rateOverTime = 0;
        }
        else
        {
            _psEmission.rateOverTime = _particlesRateOverTime;

            float particlesYRotation;
            float particlesZRotation;

            if (_horizontalInput < 0)
            {
                particlesYRotation = 180;
                particlesZRotation = Mathf.Abs(_currentAngle) * Mathf.Sign(_currentAngle);
            }
            else
            {
                particlesYRotation = 0;
                particlesZRotation = -Mathf.Abs(_currentAngle) * Mathf.Sign(_currentAngle);
            }

            ps.transform.rotation = Quaternion.Euler(0, particlesYRotation, particlesZRotation);
        }
    }
    
    //PHYSICS FUNCTIONS

    private void MovePlayer()
    {
        //Everything is doable only if we are grounded
        if (_isGrounded)
        {
            float newMoveSpeed = CalculateNewMoveSpeed();

            //We apply the calculated speed
            _rb.velocity = _moveDirection * newMoveSpeed;
        }
        else
        {
            //We limit the falling speed
            _rb.velocity = new Vector2(_rb.velocity.x, Mathf.Max(-maxFallingSpeed, _rb.velocity.y));
        }

        if (_jumpInput)
        {
            Jump();
        }
    }

    private float CalculateNewMoveSpeed()
    {
        _targetSpeed = Mathf.Abs(_horizontalInput * maxSpeed);

        //We store the velocity to limit the number of accesses
        Vector2 velocity = _rb.velocity;

        //We calculate the new velocity
        float projectedSpeed = Vector2.Dot(velocity, _moveDirection);

        float newMoveSpeed;
        if (Mathf.Abs(_horizontalInput) > minimumInput)
        {
            float step = accelerationPerSecond;
            
            if (Mathf.Sign(_horizontalInput) != Mathf.Sign(projectedSpeed))
            {
                step = Mathf.Max(slowPerSecond, accelerationPerSecond);
            }

            //Calculate the slope coefficient
            float slopeCoeff;
            if (Mathf.Sign(_horizontalInput) != Mathf.Sign(_currentAngle))
            {
                slopeCoeff = (maxWalkableAngle - Mathf.Abs(_currentAngle)) / maxWalkableAngle;
            }
            else
            {
                slopeCoeff = (maxWalkableAngle + Mathf.Abs(_currentAngle)) / maxWalkableAngle;
            }
            
            //Apply the slope coefficient to the step
            step *= slopeCoeff;

            newMoveSpeed =
                Mathf.Clamp(projectedSpeed + Time.fixedDeltaTime * step * _horizontalInput,
                    -_targetSpeed, _targetSpeed);
        }
        else
        {
            if (velocity.x < 0)
            {
                newMoveSpeed = projectedSpeed + Time.fixedDeltaTime * slowPerSecond;
            }
            else
            {
                newMoveSpeed = projectedSpeed - Time.fixedDeltaTime * slowPerSecond;
            }

            if (Mathf.Approximately(newMoveSpeed, 0))
            {
                newMoveSpeed = 0;
            }
        }

        return newMoveSpeed;
    }

    private Sequence SetupJumpSequence()
    {
        Sequence mySequence = DOTween.Sequence();
        
        //Setup movement
        mySequence.Append(body.DOLocalMove(new Vector3(0, bodyMaxShift, 0), jumpSequenceDuration / 3).SetEase(Ease.Linear)
            .OnComplete(() => _allowJump = true));
        mySequence.Append(body.DOLocalMove(new Vector3(0, -bodyMaxShift, 0), jumpSequenceDuration / 3).SetEase(Ease.Linear));
        mySequence.Append(body.DOLocalMove(new Vector3(0, 0, 0), jumpSequenceDuration / 3).SetEase(Ease.Linear));
        
        //Setup scale
        mySequence.Insert(jumpSequenceDuration / 3,
            body.DOScale(new Vector3(bodyMaxInflate, bodyMaxInflate, 1), jumpSequenceDuration / 3).SetEase(Ease.Linear));
        mySequence.Insert(jumpSequenceDuration * 2 / 3,
            body.DOScale(new Vector3(1, 1, 1), jumpSequenceDuration / 3).SetEase(Ease.Linear));

        mySequence.OnComplete(() => _allowJump = false);

        return mySequence;
    }
    
    private void Jump()
    {
        Sequence jumpSequence = SetupJumpSequence();
        
        jumpSequence.Play();
    }

    private void CheckGrounded()
    {
        Vector2 playerPos = transform.position;

        List<RaycastHit2D> hits = LaunchRaycasts(playerPos, _checkDistance);

        _isGrounded = false;
        _currentAngle = 0;

        if (hits.Count > 0)
        {
            RaycastHit2D? closestHit = FindClosestHit(hits, playerPos);

            float minAngle = FindMinAngle(hits);
            
            if (Mathf.Abs(minAngle) < maxWalkableAngle && closestHit.HasValue)
            {
                _isGrounded = true;
                _currentAngle = minAngle;
                _moveDirection = Vector3.Cross(closestHit.Value.normal, Vector3.forward).normalized;
            }
        }
    }
    
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_allowJump && col.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            List<RaycastHit2D> hits = LaunchRaycasts(body.transform.position, _jumpCheckDistance);

            if (hits.Count > 0)
            {
                Vector2 hitsMeanPoint = CalculateMeanHitPoint(hits);
                
                _rb.AddForce(((Vector2)body.transform.position - hitsMeanPoint).normalized * jumpForce, ForceMode2D.Impulse);

                _allowJump = false;
            }
        }
    }

    private void AdaptGravity()
    {
        if (_isGrounded)
        {
            _rb.gravityScale = 0;
        }
        else
        {
            _rb.gravityScale = 1;
        }
    }

    //UTILITARY FUNCTIONS
    
    private List<RaycastHit2D> LaunchRaycasts(Vector2 startPos, float distance)
    {
        Vector2 currentCheckDirection = Vector2.down;

        List<RaycastHit2D> hits = new List<RaycastHit2D>();
        for (int i = 0; i < raycastCount; i++)
        {
            RaycastHit2D currentHit = Physics2D.Raycast(startPos, currentCheckDirection, distance,
                1 << LayerMask.NameToLayer("Ground"));

            if (currentHit)
            {
                hits.Add(currentHit);
            }

            //Rotate the currentDirection
            currentCheckDirection = Quaternion.AngleAxis(360f / raycastCount, Vector3.forward) * currentCheckDirection;
        }

        return hits;
    }

    private Vector2 CalculateMeanHitPoint(List<RaycastHit2D> hits)
    {
        Vector2 hitsMeanPoint = Vector2.zero;
        foreach (RaycastHit2D hit in hits)
        {
            //We increase the hitsMeanPoint (to divide it in the end by the number of hits)
            hitsMeanPoint += hit.point;
        }

        hitsMeanPoint /= hits.Count;

        return hitsMeanPoint;
    }

    private RaycastHit2D? FindClosestHit(List<RaycastHit2D> hits, Vector2 referencePoint)
    {
        RaycastHit2D? closestHit = null;
        float closestHitDistance = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            //We calculate the currentHit's distance
            float currentHitDistance = Vector2.Distance(hit.point, referencePoint);

            if (currentHitDistance < closestHitDistance)
            {
                //If the current hit is closer than the previous ones, we update the closestHit
                closestHit = hit;
                closestHitDistance = currentHitDistance;
            }
        }
        
        return closestHit;
    }

    private float FindMinAngle(List<RaycastHit2D> hits)
    {
        float minAngleNorm = float.MaxValue;
        float minAngle = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            float currentAngle = Vector3.SignedAngle(Vector2.up, hit.normal, Vector3.forward);
            if (Mathf.Abs(currentAngle) < minAngleNorm)
            {
                minAngleNorm = Mathf.Abs(currentAngle);
                minAngle = currentAngle;
            }
        }

        return -minAngle;
    }
}