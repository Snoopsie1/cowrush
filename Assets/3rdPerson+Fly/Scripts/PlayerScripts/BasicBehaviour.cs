﻿using System.Collections.Generic;
using UnityEngine;

// This class manages which player behaviour is active or overriding, and call its local functions.
// Contains basic setup and common functions used by all the player behaviours.
public class BasicBehaviour : MonoBehaviour
{
    public Transform playerCamera; // Reference to the camera that focus the player.
    public float turnSmoothing = 0.06f; // Speed of turn when moving to match camera facing.
    public float sprintFOV = 100f; // the FOV to use on the camera when player is sprinting.
    public string sprintButton = "Sprint"; // Default sprint button input name.
    private int behaviourLocked; // Reference to temporary locked behaviour that forbids override.
    private List<GenericBehaviour> behaviours; // The list containing all the enabled player behaviours.
    private bool changedFOV; // Boolean to store when the sprint action has changed de camera FOV.
    private Vector3 colExtents; // Collider extents for ground test. 
    private int currentBehaviour; // Reference to the current player behaviour.
    private int groundedBool; // Animator variable related to whether or not the player is on the ground.

    private int hFloat; // Animator variable related to Horizontal Axis.
    private Vector3 lastDirection; // Last direction the player was moving.
    private List<GenericBehaviour> overridingBehaviours; // List of current overriding behaviours.
    private bool sprint; // Boolean to determine whether or not the player activated the sprint mode.
    private int vFloat; // Animator variable related to Vertical Axis.

    // Get current horizontal and vertical axes.
    public float GetH { get; private set; }

    public float GetV { get; private set; }

    // Get the player camera script.
    public ThirdPersonOrbitCamBasic GetCamScript { get; private set; }

    // Get the player's rigid body.
    public Rigidbody GetRigidBody { get; private set; }

    // Get the player's animator controller.
    public Animator GetAnim { get; private set; }

    // Get current default behaviour.
    public int GetDefaultBehaviour { get; private set; }

    private void Awake()
    {
        // Set up the references.
        behaviours = new List<GenericBehaviour>();
        overridingBehaviours = new List<GenericBehaviour>();
        GetAnim = GetComponent<Animator>();
        hFloat = Animator.StringToHash("H");
        vFloat = Animator.StringToHash("V");
        GetCamScript = playerCamera.GetComponent<ThirdPersonOrbitCamBasic>();
        GetRigidBody = GetComponent<Rigidbody>();

        // Grounded verification variables.
        groundedBool = Animator.StringToHash("Grounded");
        colExtents = GetComponent<Collider>().bounds.extents;
    }

    private void Update()
    {
        // Store the input axes.
        GetH = Input.GetAxis("Horizontal");
        GetV = Input.GetAxis("Vertical");

        // Set the input axes on the Animator Controller.
        GetAnim.SetFloat(hFloat, GetH, 0.1f, Time.deltaTime);
        GetAnim.SetFloat(vFloat, GetV, 0.1f, Time.deltaTime);

        // Toggle sprint by input.
        sprint = Input.GetButton(sprintButton);

        // Set the correct camera FOV for sprint mode.
        if (IsSprinting())
        {
            changedFOV = true;
            GetCamScript.SetFOV(sprintFOV);
        }
        else if (changedFOV)
        {
            GetCamScript.ResetFOV();
            changedFOV = false;
        }

        // Set the grounded test on the Animator Controller.
        GetAnim.SetBool(groundedBool, IsGrounded());
    }

    // Call the FixedUpdate functions of the active or overriding behaviours.
    private void FixedUpdate()
    {
        // Call the active behaviour if no other is overriding.
        var isAnyBehaviourActive = false;
        if (behaviourLocked > 0 || overridingBehaviours.Count == 0)
        {
            foreach (var behaviour in behaviours)
                if (behaviour.isActiveAndEnabled && currentBehaviour == behaviour.GetBehaviourCode())
                {
                    isAnyBehaviourActive = true;
                    behaviour.LocalFixedUpdate();
                }
        }
        // Call the overriding behaviours if any.
        else
        {
            foreach (var behaviour in overridingBehaviours) behaviour.LocalFixedUpdate();
        }

        // Ensure the player will stand on ground if no behaviour is active or overriding.
        if (!isAnyBehaviourActive && overridingBehaviours.Count == 0)
        {
            GetRigidBody.useGravity = true;
            Repositioning();
        }
    }

    // Call the LateUpdate functions of the active or overriding behaviours.
    private void LateUpdate()
    {
        // Call the active behaviour if no other is overriding.
        if (behaviourLocked > 0 || overridingBehaviours.Count == 0)
        {
            foreach (var behaviour in behaviours)
                if (behaviour.isActiveAndEnabled && currentBehaviour == behaviour.GetBehaviourCode())
                    behaviour.LocalLateUpdate();
        }
        // Call the overriding behaviours if any.
        else
        {
            foreach (var behaviour in overridingBehaviours) behaviour.LocalLateUpdate();
        }
    }

    // Put a new behaviour on the behaviours watch list.
    public void SubscribeBehaviour(GenericBehaviour behaviour)
    {
        behaviours.Add(behaviour);
    }

    // Set the default player behaviour.
    public void RegisterDefaultBehaviour(int behaviourCode)
    {
        GetDefaultBehaviour = behaviourCode;
        currentBehaviour = behaviourCode;
    }

    // Attempt to set a custom behaviour as the active one.
    // Always changes from default behaviour to the passed one.
    public void RegisterBehaviour(int behaviourCode)
    {
        if (currentBehaviour == GetDefaultBehaviour) currentBehaviour = behaviourCode;
    }

    // Attempt to deactivate a player behaviour and return to the default one.
    public void UnregisterBehaviour(int behaviourCode)
    {
        if (currentBehaviour == behaviourCode) currentBehaviour = GetDefaultBehaviour;
    }

    // Attempt to override any active behaviour with the behaviours on queue.
    // Use to change to one or more behaviours that must overlap the active one (ex.: aim behaviour).
    public bool OverrideWithBehaviour(GenericBehaviour behaviour)
    {
        // Behaviour is not on queue.
        if (!overridingBehaviours.Contains(behaviour))
        {
            // No behaviour is currently being overridden.
            if (overridingBehaviours.Count == 0)
                // Call OnOverride function of the active behaviour before overrides it.
                foreach (var overriddenBehaviour in behaviours)
                    if (overriddenBehaviour.isActiveAndEnabled &&
                        currentBehaviour == overriddenBehaviour.GetBehaviourCode())
                    {
                        overriddenBehaviour.OnOverride();
                        break;
                    }

            // Add overriding behaviour to the queue.
            overridingBehaviours.Add(behaviour);
            return true;
        }

        return false;
    }

    // Attempt to revoke the overriding behaviour and return to the active one.
    // Called when exiting the overriding behaviour (ex.: stopped aiming).
    public bool RevokeOverridingBehaviour(GenericBehaviour behaviour)
    {
        if (overridingBehaviours.Contains(behaviour))
        {
            overridingBehaviours.Remove(behaviour);
            return true;
        }

        return false;
    }

    // Check if any or a specific behaviour is currently overriding the active one.
    public bool IsOverriding(GenericBehaviour behaviour = null)
    {
        if (behaviour == null)
            return overridingBehaviours.Count > 0;
        return overridingBehaviours.Contains(behaviour);
    }

    // Check if the active behaviour is the passed one.
    public bool IsCurrentBehaviour(int behaviourCode)
    {
        return currentBehaviour == behaviourCode;
    }

    // Check if any other behaviour is temporary locked.
    public bool GetTempLockStatus(int behaviourCodeIgnoreSelf = 0)
    {
        return behaviourLocked != 0 && behaviourLocked != behaviourCodeIgnoreSelf;
    }

    // Atempt to lock on a specific behaviour.
    //  No other behaviour can overrhide during the temporary lock.
    // Use for temporary transitions like jumping, entering/exiting aiming mode, etc.
    public void LockTempBehaviour(int behaviourCode)
    {
        if (behaviourLocked == 0) behaviourLocked = behaviourCode;
    }

    // Attempt to unlock the current locked behaviour.
    // Use after a temporary transition ends.
    public void UnlockTempBehaviour(int behaviourCode)
    {
        if (behaviourLocked == behaviourCode) behaviourLocked = 0;
    }

    // Common functions to any behaviour:

    // Check if player is sprinting.
    public virtual bool IsSprinting()
    {
        return sprint && IsMoving() && CanSprint();
    }

    // Check if player can sprint (all behaviours must allow).
    public bool CanSprint()
    {
        foreach (var behaviour in behaviours)
            if (!behaviour.AllowSprint())
                return false;
        foreach (var behaviour in overridingBehaviours)
            if (!behaviour.AllowSprint())
                return false;
        return true;
    }

    // Check if the player is moving on the horizontal plane.
    public bool IsHorizontalMoving()
    {
        return GetH != 0;
    }

    // Check if the player is moving.
    public bool IsMoving()
    {
        return GetH != 0 || GetV != 0;
    }

    // Get the last player direction of facing.
    public Vector3 GetLastDirection()
    {
        return lastDirection;
    }

    // Set the last player direction of facing.
    public void SetLastDirection(Vector3 direction)
    {
        lastDirection = direction;
    }

    // Put the player on a standing up position based on last direction faced.
    public void Repositioning()
    {
        if (lastDirection != Vector3.zero)
        {
            lastDirection.y = 0;
            var targetRotation = Quaternion.LookRotation(lastDirection);
            var newRotation = Quaternion.Slerp(GetRigidBody.rotation, targetRotation, turnSmoothing);
            GetRigidBody.MoveRotation(newRotation);
        }
    }

    // Function to tell whether or not the player is on ground.
    public bool IsGrounded()
    {
        var ray = new Ray(transform.position + Vector3.up * 2 * colExtents.x, Vector3.down);
        return Physics.SphereCast(ray, colExtents.x, colExtents.x + 0.2f);
    }
}

// This is the base class for all player behaviours, any custom behaviour must inherit from this.
// Contains references to local components that may differ according to the behaviour itself.
public abstract class GenericBehaviour : MonoBehaviour
{
    protected int behaviourCode; // The code that identifies a behaviour.
    protected BasicBehaviour behaviourManager; // Reference to the basic behaviour manager.

    protected bool canSprint; // Boolean to store if the behaviour allows the player to sprint.

    //protected Animator anim;                       // Reference to the Animator component.
    protected int speedFloat; // Speed parameter on the Animator.

    private void Awake()
    {
        // Set up the references.
        behaviourManager = GetComponent<BasicBehaviour>();
        speedFloat = Animator.StringToHash("Speed");
        canSprint = true;

        // Set the behaviour code based on the inheriting class.
        behaviourCode = GetType().GetHashCode();
    }

    // Protected, virtual functions can be overridden by inheriting classes.
    // The active behaviour will control the player actions with these functions:

    // The local equivalent for MonoBehaviour's FixedUpdate function.
    public virtual void LocalFixedUpdate()
    {
    }

    // The local equivalent for MonoBehaviour's LateUpdate function.
    public virtual void LocalLateUpdate()
    {
    }

    // This function is called when another behaviour overrides the current one.
    public virtual void OnOverride()
    {
    }

    // Get the behaviour code.
    public int GetBehaviourCode()
    {
        return behaviourCode;
    }

    // Check if the behaviour allows sprinting.
    public bool AllowSprint()
    {
        return canSprint;
    }
}