using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterBehaviour : MonoBehaviour
{
    [SerializeField] private GameObject _skillTree;
    [SerializeField] private Transform _pfAlly;
    private SkillController _playerSkills;
    private Transform _pfProjectile;
    private float _projectileSpeed;

    private void Awake()
    {    
        // Setup skills for the character
        _playerSkills = new SkillController();
        _playerSkills.OnskillUnlocked += PlayerSkills_OnSkillUnlocked;
        
        // Get projectile prefab from resources
        _pfProjectile = Resources.Load<Transform>("Prefabs/Projectile");
    }
    private void PlayerSkills_OnSkillUnlocked(object sender, SkillController.OnSkillUnlockedEventArgs e)
    {
        // On skill upgade, change the projectile speed
        switch (e.spellType) 
        {
            case SkillController.SpellType.Projectile:
                _projectileSpeed = 10f;
                break;
            case SkillController.SpellType.ProjectileUpgrade:
                _projectileSpeed = 20f;
                break;
        }
    }
    public SkillController GetPlayerSkills()
    {
        return _playerSkills;
    }

    private void Update()
    {
        this.transform.LookAt(EyeLevelOfMousePosition());
        
        // Check if the key, binded to Skill1 has been clicked
        if (InputManager._instance.GetKeyDown(KeybindingActions.Skill1))
        {
            if (_playerSkills.IsSkillUnlocked(SkillController.SpellType.Projectile))
            {
                // Instantiate object (projectile) with direction, location and max travel length.
                Transform projectileTransform = Instantiate(_pfProjectile, this.transform.GetChild(1).position, this.transform.rotation); ;
                Vector3 shootDir = this.transform.forward;
                float distance = Vector3.Distance(EyeLevelOfMousePosition(), this.transform.position);
                projectileTransform.GetComponent<Projectile>().Setup(shootDir, distance, this.gameObject, _projectileSpeed);
            }

        }
        // Check if the key, binded to Skill2 has been clicked
        if (InputManager._instance.GetKeyDown(KeybindingActions.Skill2))
        {
            if (_playerSkills.IsSkillUnlocked(SkillController.SpellType.SpawnMonster)) 
            {
                // Instantiate object (ally) and make sure that only one instance of the object exists
                if (GameObject.Find(_pfAlly.name + "(Clone)") != null)
                {
                    Object.Destroy(GameObject.Find(_pfAlly.name + "(Clone)"));
                }
                Transform ally = Instantiate(_pfAlly, EyeLevelOfMousePosition(), Quaternion.Inverse(this.transform.rotation));
            }
        }
    }
    private Vector3 EyeLevelOfMousePosition()
    {
        // Get position of mouse in GameWorld
        Vector3 currentMousePosition = MousePosition.GetMouseWorldPosition;
        currentMousePosition.y = this.GetComponent<Collider>().bounds.size.y - 1;
        return currentMousePosition;
    } 
}
public class SkillController
{
    // All possible spells, this character can have
    public enum SpellType
    {
        None,
        Projectile,
        ProjectileUpgrade,
        ProjectileTrail,
        SpawnAlly,
    }
    public event EventHandler<OnSkillUnlockedEventArgs> OnskillUnlocked;
    public class OnSkillUnlockedEventArgs : EventArgs
    {
        public SpellType spellType;
    }

    private List<SpellType> unlockedSkillTypeList;

    public SkillController()
    {
        unlockedSkillTypeList = new List<SpellType>();
    }

    private void UnlockSkill(SpellType skillType)
    {
        // Unlock selected skill for character 
        if (!IsSkillUnlocked(skillType))
        {
            unlockedSkillTypeList.Add(skillType);
            OnskillUnlocked?.Invoke(this, new OnSkillUnlockedEventArgs { spellType = skillType });
        }        
    }

    public bool IsSkillUnlocked(SpellType skillType)
    {
        return unlockedSkillTypeList.Contains(skillType);
    }

    public SpellType GetSkillRequirement(SpellType skillType)
    {
        // Some skills can't be unlock before another skill have been unlocked 
        switch (skillType)
        {
            case SpellType.ProjectileUpgrade: return SpellType.Projectile;
            case SpellType.ProjectileTrail: return SpellType.Projectile;
        }
        return SpellType.None;
    }

    public bool TryUnlockSkill(SpellType skillType)
    {
        SpellType skillRequirement = GetSkillRequirement(skillType);

        if(skillRequirement != spellType.None && IsSkillUnlocked(skillRequirement))
        {
            return false;
        }

        UnlockSkill(skillType);
        return true;
    }
}
public class Projectile : MonoBehaviour
{
    // Serialize
    [SerializeField] private Transform _prefabPoint;
    [SerializeField] private Transform _prefabLine;
    [SerializeField] private Transform _prefabExplosion;

    // Projectile stats
    private float _moveSpeed;
    private bool _enabledProjectileTrail;

    // Setup
    private GameObject _sender;
    private Vector3 _shootDir;
    private Vector3 _lastPosition;
    private float _maxTravelLength;
    
    // Dynamic
    private float _distanceTravelled;
    private Transform _endPoint;
    
    
    public void Setup(Vector3 shootDir, float maxTravelLength, GameObject sender, float moveSpeed)
    {
        _shootDir = shootDir;
        _maxTravelLength = maxTravelLength;
        _sender = sender;
        _lastPosition = transform.position;        
        _moveSpeed = moveSpeed;

        this.GetSenderSettings();

        if (_enabledProjectileTrail)
        {
            _prefabPoint = Instantiate(_prefabPoint, this.gameObject.transform.position, Quaternion.Inverse(this.transform.rotation));
            _endPoint = Instantiate(_prefabPoint, this.gameObject.transform.position, Quaternion.Inverse(this.transform.rotation));
            _prefabLine = Instantiate(_prefabLine, this.gameObject.transform.position, Quaternion.identity);
            _prefabLine.GetComponent<LineController>().SetUpLine(new Transform[]{_prefabPoint, _endPoint}, _shootDir);
        }      
    }
    private void GetSenderSettings()
    {
        // Change this objects behavior, depending on the sender   
        var scriptName = _sender.GetComponent<CharacterBehaviour>();
        var playerSkills = scriptName.GetPlayerSkills();
        _enabledProjectileTrail = playerSkills.IsSkillUnlocked(SkillController.SpellType.ProjectileTrail);
    }
    private void Update()
    {
        if (_distanceTravelled >= _maxTravelLength)
        {
            Object.Destroy(this.gameObject);
        }

        // Slowly move this object closer to the desired posision
        transform.position += _shootDir * _moveSpeed * Time.deltaTime;
        _distanceTravelled += Vector3.Distance(transform.position, _lastPosition);
        _lastPosition = transform.position;

        // Make the trail, follow the projectile 
        if (_enabledProjectileTrail)
        {
            _endPoint.position =_lastPosition;
        }        
    }
    private void OnCollisionEnter(Collision collision)
    {
        // On hit with sender, ignore the hit. 
        if (collision.gameObject == _sender)
        {
            Physics.IgnoreCollision(_sender.GetComponent<Collider>(), GetComponent<Collider>());
        }
        else
            Object.Destroy(this.gameObject);
    }
}
