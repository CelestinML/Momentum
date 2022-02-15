using System;
using System.Collections.Generic;
using System.Linq;
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
    public float wheelMaxInflate = 2;

    [Range(0, 1)]
    public float wheelMaxShift = .3f;
    
    public float jumpSequenceDuration = .3f;

    private bool _lockCurrentJumpForce = false;
    
    private Vector2 currentJumpForce = Vector2.zero;
    
    //Private
    
    //Inputs
    private bool _currentJumpInput = false;
    private bool _previousJumpInput = false;
    private bool _jumpInput = false;

    private bool _allowJump = true;
    
    //To make sure we check the positive jump input in the next fixed update
    private bool _jumpInputHasBeenChecked = true;
    
    //Calculated according to the wheel's size
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
    
    [Range(8, 1440)] [SerializeField] private int raycastCount = 360;

    [SerializeField] private Transform groundCheck;
    
    //Calculated check distance
    private float _checkDistance;
    
    //Checks if the player is on the ground or not
    private bool _isGrounded = true;


    [Header("GFX")]

    //Serialized

    public float maxWheelRotationSpeed = 540;

    [SerializeField] private Transform wheelGfx;

    [SerializeField] private Transform shellGfx;

    [SerializeField] private float flipThreshold = 1;
    
    //Private
    
    //Stored renderer
    private SpriteRenderer _renderer;
    
    //Store current angle to rotate player accordingly
    private float _currentAngle = 0;
    

    [Header("Particle System")] 
    
    [SerializeField] private ParticleSystem walkPs;

    //Defines where the walk particles will be emitted from
    [SerializeField] private Transform playerBottom;

    //Stores the particle system's emission system to be able to modify the emission rate
    private ParticleSystem.EmissionModule _walkPsEmission;

    //Stores the original particle rate over time
    private float _particlesRateOverTime;
    
    [SerializeField] private ParticleSystem jumpPs;

    private ParticleSystem.ShapeModule _jumpPsShape;
    

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();

        _renderer = GetComponent<SpriteRenderer>();

        _checkDistance = Vector2.Distance(transform.position, groundCheck.position);

        _walkPsEmission = walkPs.emission;
        _particlesRateOverTime = (int) _walkPsEmission.rateOverTime.constant;

        _jumpPsShape = jumpPs.shape;
        jumpPs.Stop();

        _jumpCheckDistance = shellGfx.GetComponent<Renderer>().bounds.size.x / 2 * wheelMaxInflate + wheelMaxShift;
    }

    private void Update()
    {
        GetInputs();

        OrientatePlayer();

        RotateWheel();

        HandleParticles();
    }
    
    private void FixedUpdate()
    {
        CheckGrounded();
        
        HandleGravity();
        
        CalculateJump();
        
        HandleMovement();
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

    private void RotateWheel()
    {
        float projectedSpeed = Vector2.Dot(_rb.velocity, _moveDirection);
        
        wheelGfx.rotation *= Quaternion.AngleAxis(-Mathf.Sign(projectedSpeed) * _horizontalInput * maxWheelRotationSpeed * Time.deltaTime, Vector3.forward);
    }

    private void HandleParticles()
    {
        walkPs.transform.position = playerBottom.position;

        if (Mathf.Approximately(_horizontalInput, 0) || !_isGrounded)
        {
            _walkPsEmission.rateOverTime = 0;
        }
        else
        {
            _walkPsEmission.rateOverTime = _particlesRateOverTime;

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

            walkPs.transform.rotation = Quaternion.Euler(0, particlesYRotation, particlesZRotation);
        }
    }
    
    //PHYSICS FUNCTIONS

    private void HandleMovement()
    {
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

        if (_lockCurrentJumpForce)
        {
            _rb.AddForce(currentJumpForce, ForceMode2D.Impulse);
            _lockCurrentJumpForce = false;
        }
    }

    private void CalculateJump()
    {
        if (!_lockCurrentJumpForce)
        {
            currentJumpForce = Vector2.zero;
        }
        
        if (_jumpInput && _allowJump)
        {
            InflateWheel();
        }

        _jumpInputHasBeenChecked = true;
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

    private Sequence SetupInflateSequence()
    {
        Sequence mySequence = DOTween.Sequence();
        
        //Setup movement
        mySequence.Append(wheelGfx.DOLocalMove(new Vector3(0, wheelMaxShift, 0), jumpSequenceDuration / 3).SetEase(Ease.Linear));
        mySequence.Append(wheelGfx.DOLocalMove(new Vector3(0, -wheelMaxShift, 0), jumpSequenceDuration / 3).SetEase(Ease.Linear)
            .OnComplete(() => _allowJump = true));
        mySequence.Append(wheelGfx.DOLocalMove(new Vector3(0, 0, 0), jumpSequenceDuration / 3).SetEase(Ease.Linear));
        
        //Setup scale
        mySequence.Insert(jumpSequenceDuration / 3,
            shellGfx.DOScale(new Vector3(wheelMaxInflate, wheelMaxInflate, 1), jumpSequenceDuration / 3).SetEase(Ease.Linear)
                .OnComplete(Jump));
        mySequence.Insert(jumpSequenceDuration * 2 / 3,
            shellGfx.DOScale(new Vector3(1, 1, 1), jumpSequenceDuration / 3).SetEase(Ease.Linear));

        return mySequence;
    }

    private void Jump()
    {
        List<RaycastHit2D> hits = LaunchRaycasts(transform.position, _jumpCheckDistance, 90, 270);

        if (hits.Count > 0)
        {
            //Vector2 hitsMeanPoint = CalculateMeanHitPoint(hits);

            //Vector2 jumpDirection = ((Vector2) transform.position - hitsMeanPoint).normalized;

            Vector2 jumpDirection = CalculateMeanNormal(hits);

            currentJumpForce = jumpDirection * jumpForce;

            _lockCurrentJumpForce = true;
            
            //Handle jump GFX
            _jumpPsShape.rotation = new Vector3(0, -Vector3.SignedAngle(Vector2.up, jumpDirection, Vector3.forward), 0);
            //jumpPs.transform.position = hitsMeanPoint;
            jumpPs.transform.position = (Vector2)wheelGfx.position - jumpDirection * _checkDistance;
            jumpPs.Play();
        }
    }
    
    private void InflateWheel()
    {
        _allowJump = false;
        
        Sequence inflateSequence = SetupInflateSequence();
        
        inflateSequence.Play();
    }

    private void CheckGrounded()
    {
        Vector2 playerPos = transform.position;

        List<RaycastHit2D> hits = LaunchRaycasts(playerPos, _checkDistance);
        
        _isGrounded = false;
        _currentAngle = 0;
        _moveDirection = Vector2.right;

        if (hits.Count > 0)
        {
            RaycastHit2D? minAngleHit = FindMinAngleHit(hits);

            if (minAngleHit.HasValue)
            {
                float angle = -Vector3.SignedAngle(Vector2.up, minAngleHit.Value.normal, Vector3.forward);

                if (Mathf.Abs(angle) < maxWalkableAngle)
                {
                    _isGrounded = true;
                    _currentAngle = angle;
                    _moveDirection = Vector3.Cross(minAngleHit.Value.normal, Vector3.forward).normalized;
                }
            }
        }
    }

    private void HandleGravity()
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
    
    private List<RaycastHit2D> LaunchRaycasts(Vector2 startPos, float distance, float from = 180, float to = 540)
    {
        Vector2 currentCheckDirection = Vector2.up;
        currentCheckDirection = Quaternion.AngleAxis(from, Vector3.forward) * currentCheckDirection;

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
            currentCheckDirection = Quaternion.AngleAxis((to - from) / raycastCount, Vector3.forward) * currentCheckDirection;
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

    private Vector2 CalculateMeanNormal(List<RaycastHit2D> hits)
    {
        HashSet<Vector2> normals = new HashSet<Vector2>();
        Vector2 meanNormal = Vector2.zero;

        foreach (RaycastHit2D hit in hits)
        {
            if (normals.Add(hit.normal))
            {
                meanNormal += hit.normal;
            }
        }

        int normalsCount = normals.Count;
        if (normalsCount > 0)
        {
            meanNormal /= normalsCount;
        }

        return meanNormal;
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

    private RaycastHit2D? FindMinAngleHit(List<RaycastHit2D> hits)
    {
        RaycastHit2D? minAngleHit = null;
        
        float minAngleNorm = float.MaxValue;
        float minAngle = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            float currentAngle = Vector3.SignedAngle(Vector2.up, hit.normal, Vector3.forward);
            if (Mathf.Abs(currentAngle) < minAngleNorm)
            {
                minAngleHit = hit;
                minAngleNorm = Mathf.Abs(currentAngle);
                minAngle = currentAngle;
            }
        }

        return minAngleHit;
    }
}