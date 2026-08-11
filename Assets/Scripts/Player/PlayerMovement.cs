using PurrNet;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float sprintSpeed = 4f;
    [SerializeField] private float jumpForce = 1f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float rayDistance = 1.01f;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 0.4f;
    [SerializeField] private float minLookAngle = -60f;
    [SerializeField] private float maxLookAngle = 80f;
    private float xLookAngle = 0f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    private CharacterController characterController;
    private Vector3 velocity;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        enabled = isOwner;

        if (!isOwner)
        {
            Destroy(playerCamera.gameObject);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            enabled = false;
            return;
        }

        moveAction = InputSystem.actions.FindAction("Move");
        lookAction = InputSystem.actions.FindAction("Look");
        jumpAction = InputSystem.actions.FindAction("Jump");
        sprintAction = InputSystem.actions.FindAction("Sprint");
        crouchAction = InputSystem.actions.FindAction("Crouch");
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();

        if (!isOwner)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        bool isGrounded = IsGrounded();
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        var moveDir = moveAction.ReadValue<Vector2>();

        Vector3 moveDirection = transform.right * moveDir.x + transform.forward * moveDir.y;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        float currentSpeed = sprintAction.IsPressed() ? sprintSpeed : moveSpeed;
        characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

        if (jumpAction.IsPressed() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleRotation()
    {
        var lookDir = lookAction.ReadValue<Vector2>();

        // player rotation
        var yRotation = new Vector3(0, lookDir.x, 0) * lookSensitivity;
        transform.Rotate(yRotation);

        // camera rotation
        xLookAngle = Mathf.Clamp(xLookAngle + lookDir.y * lookSensitivity, minLookAngle, maxLookAngle);
        var xRotation = new Vector3(-xLookAngle, 0, 0);
        var xEulers = Quaternion.Euler(xRotation);

        playerCamera.transform.localRotation = xEulers;
    }

    private bool IsGrounded()
    {
        RaycastHit hit;

        Debug.DrawRay(transform.position, Vector3.down * rayDistance, Color.red);

        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance))
        {
            return true;
        }

        return false;
    }

}