using Project.AI.Blackboard;
using Project.AI.Combat;
using Project.AI.Configuration;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Decision.States
{
public class AICombatState : IAIState
{
    private AIWeaponExecutor _cachedWeaponExecutor;
    private bool _hasToken;
    private float _tokenCooldownTimer;
    private float _reflankTimer;
    private EntityId _entityId;
    
    public void OnEnter(AIBlackboard blackboard)
    {
        _entityId = blackboard.gameObject.GetEntityId();

        if (!_cachedWeaponExecutor)
            _cachedWeaponExecutor = blackboard.GetComponent<AIWeaponExecutor>();
        if (_cachedWeaponExecutor)
            _cachedWeaponExecutor.OnBurstCompleted += HandleBurstCompleted;
        
        _hasToken = false;
        _tokenCooldownTimer = 0f;
        _reflankTimer = 0f;

    }

    public void OnUpdate(AIBlackboard blackboard, float deltaTime)
    {
        var config = blackboard.MovementConfig;

        if (_tokenCooldownTimer > 0f) 
            _tokenCooldownTimer -= deltaTime;
        if (_reflankTimer > 0f) 
            _reflankTimer -= deltaTime;
        
        if (!blackboard.HasLineOfSight)
        {
            ReleaseTokenIfHeld();
            blackboard.SetCombatIntent(false);
            if (blackboard.TargetPosition != Vector3.zero)
            {
                blackboard.SetMoveDestination(blackboard.TargetPosition, config.CombatRunSpeed);
            }
            
            return;
        }

        if (!_hasToken && _tokenCooldownTimer <= 0f && CombatTokenDirector.Instance)
        {
            _hasToken = CombatTokenDirector.Instance.TryAcquireToken(_entityId, CombatTokenType.RangedBurst);
        }

        if (_hasToken)
        {
            ExecuteFiringStance(blackboard, config);
        }
        else
        {
            ExecuteFlankingStance(blackboard, config);
        }
    }
    
    public void OnExit(AIBlackboard blackboard)
    {
        if (_cachedWeaponExecutor)
            _cachedWeaponExecutor.OnBurstCompleted -= HandleBurstCompleted;
        ReleaseTokenIfHeld();
        blackboard.SetCombatIntent(false);
    }
    
    private void HandleBurstCompleted()
    {
        ReleaseTokenIfHeld();
        _tokenCooldownTimer = Random.Range(1.0f, 1.6f);
    } 
    
    private void ReleaseTokenIfHeld()
    {
        if (_hasToken && CombatTokenDirector.Instance)
        {
            CombatTokenDirector.Instance.ReleaseToken(_entityId, CombatTokenType.RangedBurst);
            _hasToken = false;
        }   
    }

    private void ExecuteFiringStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        blackboard.SetCombatIntent(true);

        if (blackboard.DistanceToTarget > config.MaxCombatDistance)
            blackboard.SetMoveDestination(blackboard.TargetPosition, config.CombatRunSpeed);
        else if (blackboard.DistanceToTarget < config.MinCombatDistance)
            ExecuteRetreat(blackboard, config);
        else
            blackboard.ClearMoveDestination();
    }

    private void ExecuteFlankingStance(AIBlackboard blackboard, AIMovementConfig config)
    {
        blackboard.SetCombatIntent(false);
        if (_reflankTimer <= 0f)
        {
            _reflankTimer = Random.Range(1.5f, 2.5f);
            Vector3 flankDestination = CalculateFlankPosition(blackboard, config);
            blackboard.SetMoveDestination(flankDestination, config.CombatRunSpeed);
        }
    }

    private Vector3 CalculateFlankPosition(AIBlackboard blackboard, AIMovementConfig config)
    {
        Vector3 toSelf = blackboard.transform.position - blackboard.TargetPosition;
        toSelf.y = 0f;

        if (toSelf.sqrMagnitude < 0.01f)
            toSelf = -blackboard.transform.forward;
        else
            toSelf.Normalize();

        // Deterministic left/right distribution across squad members
        float flankAngle = ((blackboard.gameObject.GetEntityId().GetHashCode() & 1) == 0 ? 1f : -1f) * 60f;
        Vector3 flankDir = Quaternion.Euler(0f, flankAngle, 0f) * toSelf;

        Vector3 desiredPosition = blackboard.TargetPosition + flankDir * config.IdealCombatDistance;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return blackboard.transform.position; // Safe fallback
    }

    private void ExecuteRetreat(AIBlackboard blackboard, AIMovementConfig config)
    {
        Vector3 dirAwayFromTarget = blackboard.transform.position - blackboard.TargetPosition;
            
        dirAwayFromTarget.y = 0f;
        if (dirAwayFromTarget.sqrMagnitude > 0.001f)
        {
            dirAwayFromTarget.Normalize();
        }
        else
        {
            dirAwayFromTarget = -blackboard.transform.forward;
        }
        Vector3 desiredRetreatPoint = blackboard.transform.position + dirAwayFromTarget * 4.0f;

        if (NavMesh.SamplePosition(desiredRetreatPoint, out var hit, 2.0f, NavMesh.AllAreas))
            blackboard.SetMoveDestination(hit.position, config.CombatRunSpeed);
    }
}
}