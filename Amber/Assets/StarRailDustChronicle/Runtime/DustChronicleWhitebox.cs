using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Amber.StarRailDustChronicle
{
    public enum DustTeam
    {
        Player,
        Enemy
    }

    public enum DustBattleResult
    {
        NotStarted,
        Running,
        PlayerWin,
        EnemyWin,
        Draw
    }

    public sealed class DustChronicleUnit : MonoBehaviour
    {
        [Header("Editable Dust Spirit Stats")]
        [SerializeField] private DustTeam team;
        [SerializeField] private string displayName = "尘灵";
        [Min(1)]
        [SerializeField] private int maxHp = 100;
        [Min(1)]
        [SerializeField] private int attackPower = 12;

        [Header("Runtime State")]
        [SerializeField] private int currentHp = 100;
        [SerializeField] private DustChronicleUnit currentTarget;
        [SerializeField] private TextMesh label;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private DustJellyBreath jellyBreath;
        [SerializeField] private Color aliveColor = Color.white;
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private Quaternion spawnRotation = Quaternion.identity;
        [SerializeField] private Vector3 visualBaseScale = Vector3.one;
        [Header("Visual Override")]
        [SerializeField] private Mesh customModel;
        [SerializeField] private Material customMaterial;

        private DustChronicleCombatController controller;
        private Renderer cachedRenderer;
        private Collider cachedCollider;
        private Rigidbody cachedRigidbody;
        private float nextHopTime;
        private float landingLockRemaining;
        private bool isHopping;
        private float hopElapsed;
        private float hopDuration;
        private Vector3 hopStart;
        private Vector3 hopEnd;
        private bool isBouncing;
        private float bounceElapsed;
        private float bounceDuration;
        private float bounceArcHeight;
        private Vector3 bounceStart;
        private Vector3 bounceEnd;
        private Vector3 bounceDirection;
        private bool hasRuntimeSpawnPoint;

        public DustTeam Team => team;
        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public int AttackPower => attackPower;
        public DustChronicleUnit CurrentTarget => currentTarget;
        public bool IsAlive => currentHp > 0;
        public Rigidbody Body => cachedRigidbody;

        private void Awake()
        {
            EnsureJellyBreath();
        }

        public void Configure(DustTeam unitTeam, string unitName, int hitPoints, int damage, Color unitColor, Transform visual, DustChronicleCombatController owner)
        {
            team = unitTeam;
            displayName = unitName;
            maxHp = Mathf.Max(1, hitPoints);
            attackPower = Mathf.Max(1, damage);
            aliveColor = unitColor;
            visualRoot = visual;
            visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            EnsureJellyBreath();
            ApplyCustomVisual();

            controller = owner;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            hasRuntimeSpawnPoint = true;
            CacheComponents();
            ResetForBattle();
        }

        public void SetController(DustChronicleCombatController owner)
        {
            controller = owner;
        }

        public void SetLabel(TextMesh value)
        {
            label = value;
            RefreshLabel();
        }

        public void ResetForBattle()
        {
            CacheComponents();
            ApplyCustomVisual();

            if (!hasRuntimeSpawnPoint)
            {
                spawnPosition = transform.position;
                spawnRotation = transform.rotation;
                hasRuntimeSpawnPoint = true;
            }

            currentHp = Mathf.Max(1, maxHp);
            currentTarget = null;
            nextHopTime = 0f;
            landingLockRemaining = 0.1f;
            isHopping = false;
            isBouncing = false;
            hopElapsed = 0f;
            hopDuration = 0f;
            hopStart = spawnPosition;
            hopEnd = spawnPosition;
            bounceElapsed = 0f;
            bounceDuration = 0f;
            bounceArcHeight = 0f;
            bounceStart = spawnPosition;
            bounceEnd = spawnPosition;
            bounceDirection = Vector3.zero;

            if (cachedCollider != null)
            {
                cachedCollider.enabled = true;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.isKinematic = false;
                cachedRigidbody.mass = 1f;
                cachedRigidbody.useGravity = false;
                cachedRigidbody.velocity = Vector3.zero;
                cachedRigidbody.angularVelocity = Vector3.zero;
                cachedRigidbody.position = spawnPosition;
                cachedRigidbody.rotation = spawnRotation;
            }
            else
            {
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = visualBaseScale;
            }

            if (jellyBreath != null)
            {
                jellyBreath.SetBaseScale(visualBaseScale);
                jellyBreath.ResetVisual();
            }

            SetColor(aliveColor);
            RefreshLabel();
        }

        public void SetTarget(DustChronicleUnit target)
        {
            currentTarget = target;
            RefreshLabel();
        }

        public void TickAutoBattle(float fixedDeltaTime, float sharedMoveSpeed, float hopArcHeight, float hopInterval, float landingLockDuration)
        {
            if (!IsAlive)
            {
                return;
            }

            if (cachedRigidbody == null)
            {
                return;
            }

            if (isBouncing)
            {
                ContinueCollisionBounce(fixedDeltaTime);
                return;
            }

            if (currentTarget == null || !currentTarget.IsAlive)
            {
                StopHorizontalVelocity();
                return;
            }

            if (isHopping)
            {
                ContinueHop(fixedDeltaTime, hopArcHeight, landingLockDuration);
                return;
            }

            landingLockRemaining = Mathf.Max(0f, landingLockRemaining - fixedDeltaTime);

            StopHorizontalVelocity();
            if (landingLockRemaining > 0f || Time.time < nextHopTime)
            {
                return;
            }

            StartHopTowardTarget(sharedMoveSpeed, hopInterval);
        }

        public void InterruptHopAndDisplace(Vector3 horizontalDirection, float distance, float lockDuration)
        {
            if (!IsAlive)
            {
                return;
            }

            CacheComponents();

            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude <= 0.0001f)
            {
                horizontalDirection = -transform.forward;
                horizontalDirection.y = 0f;
            }

            if (horizontalDirection.sqrMagnitude <= 0.0001f)
            {
                horizontalDirection = team == DustTeam.Player ? Vector3.left : Vector3.right;
            }

            horizontalDirection.Normalize();
            isHopping = false;
            isBouncing = true;
            hopElapsed = 0f;
            hopDuration = 0f;
            bounceElapsed = 0f;
            bounceDuration = Mathf.Clamp(lockDuration, 0.12f, 0.36f);
            bounceArcHeight = Mathf.Lerp(0.16f, 0.38f, Mathf.Clamp01(distance));
            landingLockRemaining = Mathf.Max(landingLockRemaining, bounceDuration + 0.06f);
            nextHopTime = Mathf.Max(nextHopTime, Time.time + landingLockRemaining);
            StopHorizontalVelocity();

            var basePosition = cachedRigidbody != null ? cachedRigidbody.position : transform.position;
            bounceStart = basePosition;
            bounceEnd = basePosition + horizontalDirection * Mathf.Max(0f, distance);
            bounceStart.y = spawnPosition.y;
            bounceEnd.y = spawnPosition.y;
            bounceDirection = horizontalDirection;
            hopStart = bounceEnd;
            hopEnd = bounceEnd;

            if (cachedRigidbody != null)
            {
                cachedRigidbody.rotation = Quaternion.LookRotation(-horizontalDirection);
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(-horizontalDirection);
            }

            if (jellyBreath != null)
            {
                jellyBreath.Pulse(0.48f, ToVisualLocalDirection(horizontalDirection));
            }
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive)
            {
                return;
            }

            currentHp = Mathf.Max(0, currentHp - Mathf.Max(0, amount));
            if (jellyBreath != null)
            {
                jellyBreath.Pulse(0.36f);
            }

            if (!IsAlive)
            {
                Die();
            }

            RefreshLabel();
        }

        public void ApplyCustomVisual()
        {
            if (visualRoot == null)
            {
                return;
            }

            var meshFilter = visualRoot.GetComponent<MeshFilter>();
            if (meshFilter != null && customModel != null)
            {
                meshFilter.sharedMesh = customModel;
            }

            var renderer = visualRoot.GetComponent<Renderer>();
            if (renderer != null && customMaterial != null)
            {
                renderer.sharedMaterial = customMaterial;
            }
        }

        public void RefreshLabel()
        {
            if (label == null)
            {
                return;
            }

            var status = IsAlive ? "存活" : "被击败";
            var targetName = currentTarget != null && currentTarget.IsAlive ? currentTarget.DisplayName : "无";
            label.text = $"{displayName}\n{team} | {status}\nHP {currentHp}/{maxHp}  ATK {attackPower}\nTarget: {targetName}";
        }

        private void StartHopTowardTarget(float sharedMoveSpeed, float hopInterval)
        {
            var direction = currentTarget.transform.position - transform.position;
            direction.y = 0f;
            var distance = direction.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            direction /= distance;
            transform.rotation = Quaternion.LookRotation(direction);
            hopDuration = Mathf.Max(0.18f, hopInterval - landingLockRemaining);
            var stepDistance = Mathf.Min(sharedMoveSpeed * hopDuration, Mathf.Max(0.15f, distance - 0.95f));
            hopStart = cachedRigidbody.position;
            hopEnd = hopStart + direction * stepDistance;
            hopEnd.y = spawnPosition.y;
            hopElapsed = 0f;
            isHopping = true;
            nextHopTime = Time.time + Mathf.Max(0.05f, hopInterval);
        }

        private void ContinueHop(float fixedDeltaTime, float hopArcHeight, float landingLockDuration)
        {
            hopElapsed += fixedDeltaTime;
            var t = Mathf.Clamp01(hopElapsed / Mathf.Max(0.01f, hopDuration));
            var position = Vector3.Lerp(hopStart, hopEnd, t);
            position.y = Mathf.Lerp(hopStart.y, spawnPosition.y, t) + Mathf.Sin(t * Mathf.PI) * hopArcHeight;
            cachedRigidbody.MovePosition(position);

            if (t >= 1f)
            {
                isHopping = false;
                cachedRigidbody.position = hopEnd;
                OnLanded(landingLockDuration);
            }
        }

        private void ContinueCollisionBounce(float fixedDeltaTime)
        {
            bounceElapsed += fixedDeltaTime;
            var t = Mathf.Clamp01(bounceElapsed / Mathf.Max(0.01f, bounceDuration));
            var eased = EaseOutBack(t);
            var position = Vector3.LerpUnclamped(bounceStart, bounceEnd, eased);
            position.y = Mathf.Lerp(bounceStart.y, spawnPosition.y, t) + Mathf.Sin(t * Mathf.PI) * bounceArcHeight;

            cachedRigidbody.MovePosition(position);

            if (t >= 1f)
            {
                isBouncing = false;
                cachedRigidbody.position = bounceEnd;
                landingLockRemaining = Mathf.Max(landingLockRemaining, 0.06f);
                StopHorizontalVelocity();
                if (jellyBreath != null)
                {
                    jellyBreath.Pulse(0.34f, ToVisualLocalDirection(-bounceDirection));
                }
            }
        }

        private void OnLanded(float landingLockDuration)
        {
            StopHorizontalVelocity();
            landingLockRemaining = Mathf.Max(0f, landingLockDuration);
            if (jellyBreath != null)
            {
                jellyBreath.Pulse(0.42f);
            }
        }

        private void StopHorizontalVelocity()
        {
            if (cachedRigidbody == null)
            {
                return;
            }

            cachedRigidbody.velocity = new Vector3(0f, cachedRigidbody.velocity.y, 0f);
            cachedRigidbody.angularVelocity = Vector3.zero;
        }

        private void Die()
        {
            currentTarget = null;
            if (cachedCollider != null)
            {
                cachedCollider.enabled = false;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector3.zero;
                cachedRigidbody.angularVelocity = Vector3.zero;
                cachedRigidbody.isKinematic = true;
            }

            SetColor(new Color(0.18f, 0.18f, 0.18f));
        }

        private Vector3 ToVisualLocalDirection(Vector3 worldDirection)
        {
            if (visualRoot != null)
            {
                return visualRoot.InverseTransformDirection(worldDirection);
            }

            return transform.InverseTransformDirection(worldDirection);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.35f;
            var shifted = t - 1f;
            return 1f + (c1 + 1f) * shifted * shifted * shifted + c1 * shifted * shifted;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !isBouncing)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bounceStart, bounceEnd);
        }

        private void CacheComponents()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = visualRoot != null ? visualRoot.GetComponent<Renderer>() : GetComponent<Renderer>();
            }

            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }

            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            EnsureJellyBreath();
        }

        private void EnsureJellyBreath()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (visualBaseScale == Vector3.one && visualRoot.localScale != Vector3.one)
            {
                visualBaseScale = visualRoot.localScale;
            }

            if (jellyBreath == null)
            {
                jellyBreath = visualRoot.GetComponent<DustJellyBreath>();
            }

            if (jellyBreath == null)
            {
                jellyBreath = visualRoot.gameObject.AddComponent<DustJellyBreath>();
            }

            if (jellyBreath != null)
            {
                jellyBreath.SetBaseScale(visualBaseScale);
            }
        }

        private void SetColor(Color color)
        {
            if (cachedRenderer != null && cachedRenderer.sharedMaterial != null)
            {
                cachedRenderer.sharedMaterial.color = color;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryResolveDustCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryResolveDustCollision(collision);
        }

        private void TryResolveDustCollision(Collision collision)
        {
            if (!IsAlive || controller == null)
            {
                return;
            }

            var other = collision.collider.GetComponentInParent<DustChronicleUnit>();
            if (other != null && other != this)
            {
                controller.ResolveDustCollision(this, other);
            }
        }

        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            attackPower = Mathf.Max(1, attackPower);
            if (!Application.isPlaying)
            {
                currentHp = Mathf.Clamp(currentHp <= 0 ? maxHp : currentHp, 1, maxHp);
            }
        }
    }

    public sealed class DustChronicleCombatController : MonoBehaviour
    {
        [Header("Auto Battle Rules")]
        [SerializeField] private bool startBattleAutomatically = true;
        [SerializeField] private bool showDebugGui = true;
        [Min(0.1f)]
        [SerializeField] private float sharedMoveSpeed = 3.2f;
        [Min(0.1f)]
        [SerializeField] private float hopArcHeight = 1.35f;
        [Min(0.05f)]
        [SerializeField] private float hopInterval = 0.72f;
        [Min(0f)]
        [SerializeField] private float landingLockDuration = 0.18f;
        [Min(0f)]
        [SerializeField] private float collisionDamageCooldown = 0.32f;
        [Min(0f)]
        [SerializeField] private float collisionSeparationDistance = 0.72f;
        [Min(0f)]
        [SerializeField] private float postCollisionLockDuration = 0.24f;

        [Header("Whitebox References")]
        [SerializeField] private List<DustChronicleUnit> playerDustSpirits = new List<DustChronicleUnit>();
        [SerializeField] private List<DustChronicleUnit> enemyDustSpirits = new List<DustChronicleUnit>();
        [SerializeField] private TextMesh statusBoard;

        private readonly Queue<string> combatLog = new Queue<string>();
        private bool suppressAutoRebuild;
        private readonly Dictionary<int, float> collisionCooldowns = new Dictionary<int, float>();
        private DustBattleResult result = DustBattleResult.NotStarted;
        private float elapsedBattleTime;

        public DustBattleResult Result => result;
        public float SharedMoveSpeed => sharedMoveSpeed;
        public float ElapsedBattleTime => elapsedBattleTime;
        public int PlayerDustSpiritCount => playerDustSpirits != null ? playerDustSpirits.Count : 0;
        public int EnemyDustSpiritCount => enemyDustSpirits != null ? enemyDustSpirits.Count : 0;
        public bool IsBattleRunning => result == DustBattleResult.Running;
        public bool IsBattleFinished => result == DustBattleResult.PlayerWin || result == DustBattleResult.EnemyWin || result == DustBattleResult.Draw;

        public void Configure(List<DustChronicleUnit> players, List<DustChronicleUnit> enemies, TextMesh board)
        {
            playerDustSpirits = players;
            enemyDustSpirits = enemies;
            statusBoard = board;

            foreach (var unit in AllUnits())
            {
                if (unit != null)
                {
                    unit.SetController(this);
                }
            }

            ResetBattle();
        }

        public void SetSuppressAutoRebuild(bool suppress)
        {
            suppressAutoRebuild = suppress;
        }

        public void ConfigureFlowMode(bool autoStartBattle, bool debugGuiVisible)
        {
            startBattleAutomatically = autoStartBattle;
            showDebugGui = debugGuiVisible;
        }

        public void StartBattle()
        {
            if (result == DustBattleResult.Running)
            {
                return;
            }

            ResetBattle();
            result = DustBattleResult.Running;
            PushLog("战斗开始：尘灵立方体跳跃冲向敌人，碰撞后双方受到攻击力伤害。");
            RefreshAll();
        }

        public int GetAliveCount(DustTeam team)
        {
            return team == DustTeam.Player ? AliveCount(playerDustSpirits) : AliveCount(enemyDustSpirits);
        }

        public void ResetBattle()
        {
            result = DustBattleResult.NotStarted;
            elapsedBattleTime = 0f;
            combatLog.Clear();
            collisionCooldowns.Clear();
            if (!suppressAutoRebuild)
            {
                RebuildSideReferencesFromChildren();
            }

            foreach (var unit in AllUnits())
            {
                if (unit != null)
                {
                    unit.SetController(this);
                    unit.ResetForBattle();
                }
            }

            PushLog("就绪：生命和攻击可编辑；所有尘灵共享相同的质量和移动速度。");
            RefreshAll();
        }

        public void ResolveDustCollision(DustChronicleUnit a, DustChronicleUnit b)
        {
            if (result != DustBattleResult.Running || a == null || b == null || !a.IsAlive || !b.IsAlive || a.Team == b.Team)
            {
                return;
            }

            var key = CollisionKey(a, b);
            if (collisionCooldowns.TryGetValue(key, out var readyTime) && Time.time < readyTime)
            {
                return;
            }

            collisionCooldowns[key] = Time.time + collisionDamageCooldown;
            var separationDirection = b.transform.position - a.transform.position;
            separationDirection.y = 0f;
            if (separationDirection.sqrMagnitude <= 0.0001f)
            {
                separationDirection = a.Team == DustTeam.Player ? Vector3.right : Vector3.left;
            }

            b.TakeDamage(a.AttackPower);
            a.TakeDamage(b.AttackPower);
            SeparateAfterCollision(a, b, separationDirection);
            PushLog($"{a.DisplayName} collides with {b.DisplayName}: -{b.AttackPower}/-{a.AttackPower} HP.");
            ResolveBattleResult();
            RefreshAll();
        }

        private void Start()
        {
            if (startBattleAutomatically)
            {
                StartBattle();
            }
            else
            {
                ResetBattle();
            }
        }

        private void FixedUpdate()
        {
            if (result != DustBattleResult.Running)
            {
                return;
            }

            if (PlayerDustSpiritCount == 0 || EnemyDustSpiritCount == 0)
            {
                RebuildSideReferencesFromChildren();
            }

            elapsedBattleTime += Time.fixedDeltaTime;
            ResolveSide(playerDustSpirits, enemyDustSpirits);
            ResolveSide(enemyDustSpirits, playerDustSpirits);
            ResolveBattleResult();
            RefreshAll();
        }

        private void ResolveSide(List<DustChronicleUnit> actingSide, List<DustChronicleUnit> opposingSide)
        {
            foreach (var unit in actingSide)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var target = FindNearestAlive(unit, opposingSide);
                unit.SetTarget(target);
                unit.TickAutoBattle(Time.fixedDeltaTime, sharedMoveSpeed, hopArcHeight, hopInterval, landingLockDuration);
            }
        }

        private void ResolveBattleResult()
        {
            if (result != DustBattleResult.Running)
            {
                return;
            }

            var playerAlive = HasAliveUnit(playerDustSpirits);
            var enemyAlive = HasAliveUnit(enemyDustSpirits);

            if (playerAlive && enemyAlive)
            {
                return;
            }

            if (!playerAlive && !enemyAlive)
            {
                result = DustBattleResult.Draw;
                PushLog("结果：平局。双方尘灵均被击败。");
            }
            else if (playerAlive)
            {
                result = DustBattleResult.PlayerWin;
                PushLog("结果：我方尘灵胜利。");
            }
            else
            {
                result = DustBattleResult.EnemyWin;
                PushLog("结果：敌方尘灵胜利。");
            }

            ClearTargets();
            StopAllBodies();
        }

        private void ClearTargets()
        {
            foreach (var unit in AllUnits())
            {
                if (unit != null)
                {
                    unit.SetTarget(null);
                }
            }
        }

        private void StopAllBodies()
        {
            foreach (var unit in AllUnits())
            {
                if (unit != null && unit.Body != null)
                {
                    unit.Body.velocity = Vector3.zero;
                    unit.Body.angularVelocity = Vector3.zero;
                }
            }
        }

        private void RebuildSideReferencesFromChildren()
        {
            EnsureSideLists();

            var childUnits = GetComponentsInChildren<DustChronicleUnit>(true);
            if (childUnits == null || childUnits.Length == 0)
            {
                return;
            }

            playerDustSpirits.Clear();
            enemyDustSpirits.Clear();

            foreach (var unit in childUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.SetController(this);
                if (unit.Team == DustTeam.Player)
                {
                    playerDustSpirits.Add(unit);
                }
                else
                {
                    enemyDustSpirits.Add(unit);
                }
            }
        }

        private void EnsureSideLists()
        {
            if (playerDustSpirits == null)
            {
                playerDustSpirits = new List<DustChronicleUnit>();
            }

            if (enemyDustSpirits == null)
            {
                enemyDustSpirits = new List<DustChronicleUnit>();
            }
        }

        private void SeparateAfterCollision(DustChronicleUnit a, DustChronicleUnit b, Vector3 directionFromAToB)
        {
            directionFromAToB.y = 0f;
            if (directionFromAToB.sqrMagnitude <= 0.0001f)
            {
                directionFromAToB = a.Team == DustTeam.Player ? Vector3.right : Vector3.left;
            }

            directionFromAToB.Normalize();
            var separation = Mathf.Max(0f, collisionSeparationDistance);
            var lockDuration = Mathf.Max(0f, postCollisionLockDuration);

            if (a.IsAlive)
            {
                a.InterruptHopAndDisplace(-directionFromAToB, separation, lockDuration);
            }

            if (b.IsAlive)
            {
                b.InterruptHopAndDisplace(directionFromAToB, separation, lockDuration);
            }
        }

        private DustChronicleUnit FindNearestAlive(DustChronicleUnit seeker, List<DustChronicleUnit> candidates)
        {
            DustChronicleUnit nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var distance = (candidate.transform.position - seeker.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static bool HasAliveUnit(List<DustChronicleUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit != null && unit.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<DustChronicleUnit> AllUnits()
        {
            EnsureSideLists();

            foreach (var unit in playerDustSpirits)
            {
                yield return unit;
            }

            foreach (var unit in enemyDustSpirits)
            {
                yield return unit;
            }
        }

        private static int CollisionKey(DustChronicleUnit a, DustChronicleUnit b)
        {
            var first = Mathf.Min(a.GetInstanceID(), b.GetInstanceID());
            var second = Mathf.Max(a.GetInstanceID(), b.GetInstanceID());
            unchecked
            {
                return (first * 397) ^ second;
            }
        }

        private void PushLog(string line)
        {
            combatLog.Enqueue(line);
            while (combatLog.Count > 10)
            {
                combatLog.Dequeue();
            }
        }

        private void RefreshAll()
        {
            foreach (var unit in AllUnits())
            {
                if (unit != null)
                {
                    unit.RefreshLabel();
                }
            }

            if (statusBoard == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("尘灵果冻立方碰撞白盒");
            builder.AppendLine("核心循环：等速立方尘灵跳跃冲向敌人，碰撞后双方受到攻击力伤害。");
            builder.AppendLine($"状态：{result} | 时间 {elapsedBattleTime:0.0}秒 | 移速 {sharedMoveSpeed:0.00} | 跳跃高度 {hopArcHeight:0.00} | 碰撞CD {collisionDamageCooldown:0.00}秒 | 分离距离 {collisionSeparationDistance:0.00}");
            builder.AppendLine($"我方存活：{AliveCount(playerDustSpirits)}/{playerDustSpirits.Count} | 敌方存活：{AliveCount(enemyDustSpirits)}/{enemyDustSpirits.Count}");
            builder.AppendLine();
            builder.AppendLine("战斗日志：");
            foreach (var line in combatLog)
            {
                builder.AppendLine($"- {line}");
            }

            statusBoard.text = builder.ToString();
        }

        private static int AliveCount(List<DustChronicleUnit> units)
        {
            var count = 0;
            foreach (var unit in units)
            {
                if (unit != null && unit.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnGUI()
        {
            if (!showDebugGui)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16, 16, 315, 150), GUI.skin.box);
            GUILayout.Label($"战斗：{result}");
            GUILayout.Label($"等速移速：{sharedMoveSpeed:0.00}");
            GUILayout.Label("每次对立碰撞造成一次伤害。");
            if (GUILayout.Button(result == DustBattleResult.Running ? "重新战斗" : "开始战斗"))
            {
                StartBattle();
            }

            if (GUILayout.Button("重置满血"))
            {
                ResetBattle();
            }

            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            sharedMoveSpeed = Mathf.Max(0.1f, sharedMoveSpeed);
            hopArcHeight = Mathf.Max(0.1f, hopArcHeight);
            hopInterval = Mathf.Max(0.05f, hopInterval);
            landingLockDuration = Mathf.Max(0f, landingLockDuration);
            collisionDamageCooldown = Mathf.Max(0f, collisionDamageCooldown);
            collisionSeparationDistance = Mathf.Max(0f, collisionSeparationDistance);
            postCollisionLockDuration = Mathf.Max(0f, postCollisionLockDuration);
        }
    }
}
