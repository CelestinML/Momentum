using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Player statistics")]

    public float maxSpeed = 10;

    public float jumpForce = 20;

    public float slowPerSecond = 1;

    public float accelerationPerSecond = 2;

    public float maxAngle = 35;

    [Range(0, 1)] [SerializeField] private float minimumInput = 0.05f;

    [SerializeField] private float maxFallingSpeed = 25;
    
    [Header("Collision detection")]
    
    [Range(8, 360)] [SerializeField] private int raycastCount = 360;

    [SerializeField] private Transform groundCheck;

    [Header("GFX")]
    
    [SerializeField] private Transform body;

    [Header("Particle System")]
    
    [SerializeField] private ParticleSystem ps;
    
    //Defines where the particles will be emitted from
    [SerializeField] private Transform playerBottom;

    //Stores the particle system's emission system to be able to modify the emission rate
    private ParticleSystem.EmissionModule _psEmission;
    
    //Stores the original particle rate over time
    private float _particlesRateOverTime;
    
    //Calculated check distance
    private float _checkDistance;

    //Stored inputs
    private float _horizontalInput = 0;
    private bool _currentJumpInput = false;
    private bool _previousJumpInput = false;
    private bool _jumpInput = false;
    private bool _jumpInputHasBeenChecked = true;

    //Calculated target speed
    private float _targetSpeed = 0;

    //Stored rigidbody
    private Rigidbody2D _rb;

    //Stored renderer
    private SpriteRenderer _renderer;

    //Checks if the player is on the ground or not
    private bool _isGrounded = true;

    //Stores the last known ground normal
    private Vector2? _meanNormal;
    private Vector2 _moveDirection;
    
    //Store current angle to rotate player accordingly
    private float _currentAngle = 0;
    
    //Player z rotation (stored so that the particle system can copy this rotation)
    private float _zRotation;

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _renderer = GetComponent<SpriteRenderer>();

        _checkDistance = Vector2.Distance(transform.position, groundCheck.position);

        _psEmission = ps.emission;
        _particlesRateOverTime = (int) _psEmission.rateOverTime.constant;
    }

    private void Update()
    {
        GetInputs();

        OrientatePlayer();

        HandleParticles();
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
    
    private void OrientatePlayer()
    {
        float yRotation = transform.rotation.y;

        float flipThreshold = maxSpeed / 8;

        float projectedSpeed = Vector2.Dot(_rb.velocity, _moveDirection);
        
        if (projectedSpeed < -flipThreshold)
        {
            yRotation = 180;
        }
        else if (projectedSpeed > flipThreshold)
        {
            yRotation = 0;
        }

        if (yRotation == 180)
        {
            _zRotation = _currentAngle;
        }
        else
        {
            _zRotation = -_currentAngle;
        }

        transform.rotation = Quaternion.Euler(0, yRotation, _zRotation);
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
                particlesZRotation = Mathf.Abs(_zRotation);
            }
            else
            {
                particlesYRotation = 0;
                particlesZRotation = -Mathf.Abs(_zRotation);
            }
            
            ps.transform.rotation = Quaternion.Euler(0, particlesYRotation, particlesZRotation);
        }
    }

    private void FixedUpdate()
    {
        CheckGrounded();

        AdaptGravity();

        MovePlayer();

        _jumpInputHasBeenChecked = true;
    }

    private void MovePlayer()
    {
        //Everything is doable only if we are grounded
        if (_isGrounded)
        {
            float newMoveSpeed = CalculateNewMoveSpeed();

            //We apply the calculated speed
            _rb.velocity = _moveDirection * newMoveSpeed;
            
            if (_jumpInput)
            {
                Jump();
            }
        }
        else
        {
            //We limit the falling speed
            _rb.velocity = new Vector2(_rb.velocity.x, Mathf.Max(-maxFallingSpeed, _rb.velocity.y));
            
            if (_jumpInput)
            {
                Jump();
            }
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
                step = slowPerSecond;
            }
            
            newMoveSpeed =
                Mathf.Clamp( projectedSpeed + Time.fixedDeltaTime * step * _horizontalInput,
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

    private void Jump()
    {
        if (_meanNormal.HasValue)
        {
            if (!_isGrounded)
            {
                _rb.velocity = Vector2.zero;
            }
            _rb.AddForce(_meanNormal.Value * jumpForce, ForceMode2D.Impulse);
            InflateBody();
        }
    }
    
    private void InflateBody()
    {
        body.DOScale(new Vector3(2, 2, 1), .1f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.Linear);
        body.DOLocalMove(new Vector3(0, -.3f, 0), .1f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.Linear);
    }

    private void CheckGrounded()
    {
        Vector2 playerPos = transform.position;
        
        Vector2 hitsMeanPoint = Vector2.zero;

        RaycastHit2D? closestHit = null;
        float closestHitDistance = float.MaxValue;

        float minAngle = float.MaxValue;

        List<RaycastHit2D> hits = LaunchRaycasts(playerPos);

        foreach (RaycastHit2D hit in hits)
        {
            //We increase the hitsMeanPoint (to divide it in the end by the number of hits)
            hitsMeanPoint += hit.point;
            
            //We calculate the currentHit's distance
            float currentHitDistance = Vector2.Distance(hit.point, playerPos);
            
            if (currentHitDistance < closestHitDistance)
            {
                //If the current hit is closer than the previous ones, we update the closestHit
                closestHit = hit;
                closestHitDistance = currentHitDistance;
            }
            
            float currentAngle = Vector3.Angle(Vector2.up, hit.normal);
            if (currentAngle < minAngle)
            {
                minAngle = currentAngle;
            }
        }

        _isGrounded = false;
        _currentAngle = 0;
        
        if (hits.Count > 0)
        {
            if (minAngle < maxAngle && closestHit.HasValue)
            {
                _isGrounded = true;
                _currentAngle = minAngle;
                _moveDirection = Vector3.Cross(closestHit.Value.normal, Vector3.forward).normalized;
                Debug.DrawRay(transform.position, _moveDirection);
            }
            
            _meanNormal = (playerPos - hitsMeanPoint / hits.Count).normalized;
        }
        else
        {
            _meanNormal = null;
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

    private List<RaycastHit2D> LaunchRaycasts(Vector2 startPos)
    {
        Vector2 currentCheckDirection = Vector2.down;
        
        List<RaycastHit2D> hits = new List<RaycastHit2D>();
        for (int i = 0; i < raycastCount; i++)
        {
            RaycastHit2D currentHit = Physics2D.Raycast(startPos, currentCheckDirection, _checkDistance,
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
}