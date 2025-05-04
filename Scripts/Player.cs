using Fusion;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour
{

    [SerializeField] private Ball _prefabBall;
    [SerializeField] private PhysxBall _prefabPhysxBall;
    
    [Networked] public bool spawnedProjectile  { get; set; }
    public Material _material;

    private ChangeDetector _changeDetector;
    
    private NetworkCharacterController _cc;
    [Networked] private TickTimer delay { get; set; }
    
    private Vector3 _forward = Vector3.forward;

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
        _forward = transform.forward;
        _material = GetComponentInChildren<MeshRenderer>().material;
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();
            _cc.Move(5*data.direction*Runner.DeltaTime);
            
            if (data.direction.sqrMagnitude > 0)
                _forward = data.direction;
            
            if (HasStateAuthority && delay.ExpiredOrNotRunning(Runner))
            {
                if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
                {
                    delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    Runner.Spawn(_prefabBall,
                        transform.position+_forward, Quaternion.LookRotation(_forward),
                        Object.InputAuthority, (runner, o) =>
                        {
                            // Initialize the Ball before synchronizing it
                            o.GetComponent<Ball>().Init();
                        });
                    spawnedProjectile = !spawnedProjectile;
                }

                if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1))
                {
                    delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    Runner.Spawn(_prefabPhysxBall,
                        transform.position+_forward, 
                        Quaternion.LookRotation(_forward),
                        Object.InputAuthority, (runner, o) =>
                        {
                            o.GetComponent<PhysxBall>().Init(10 * _forward);   
                        }
                    );
                    spawnedProjectile = !spawnedProjectile;
                }
            }
        }
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(spawnedProjectile):
                    _material.color = Color.white;
                    break;
            }
        }
        _material.color = Color.Lerp(_material.color, Color.blue, Time.deltaTime);
    }

    private void Update()
    {
        if (Object.HasInputAuthority && Input.GetKeyDown(KeyCode.R))
        {
            RPC_SendMessage("Bonjour");
        }
    }
    
    
    //#region rpc

    private TMP_Text _messages;

    /**
        RpcSources.InputAuthority => 只有對物件具有輸入授權的客戶端才能觸發RPC來發送訊息。
        RpcTargets.StateAuthority => 發送訊息RPC被發送到主機端（狀態授權）。
        RpcHostMode.SourceIsHostPlayer => 由於主機端既是伺服器又是客戶端，因此需要指定調用RPC的主機端。
     */
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_SendMessage(string message, RpcInfo info = default)
    {
        RPC_RelayMessage(message, info.Source);
    }

    /**
        RpcSources.StateAuthority => 伺服器/主機端正在發送此RPC。
        RpcTargets.All => 所有客戶端都應接收此RPC。
        HostMode = RpcHostMode.SourceIsServer => 主機應用程式的伺服器部分正在發送此RPC。
     */
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
    public void RPC_RelayMessage(string message, PlayerRef messageSource)
    {
        if (_messages == null) _messages = FindObjectOfType<TMP_Text>();

        if (messageSource == Runner.LocalPlayer)
        {
            message = $"You said: {message}\n";
        }
        else
        {
            message = $"Some other player said : {message}\n";
        }

        _messages.text = message;
    }

    //#endregion

}
