using System;
using Project.Combat;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Development.Combat
{
[RequireComponent(typeof(TextMeshPro))]
public class DEV_DamagePopup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshPro textMesh;

    [Header("Animation Settings")]
    [SerializeField] private float moveSpeedY = 2f;
    [SerializeField] private float disappearTimer = 0.6f;
    [SerializeField] private float fadeSpeed = 3f;

    private Color textColor;
    private Transform cameraTransform;
    
    public static DEV_DamagePopup Create(Vector3 position, int damageAmount, HitLocation hitLocation, DEV_DamagePopup prefab)
    {
        Vector3 spawnPosition = position + new Vector3(
            Random.Range(-0.25f, 0.25f),
            Random.Range(0f, 0.2f),
            Random.Range(-0.25f, 0.25f)
        );

        DEV_DamagePopup damagePopup = Instantiate(prefab, spawnPosition, Quaternion.identity);
        damagePopup.Setup(damageAmount, hitLocation);
        return damagePopup;
    }

    private void Awake()
    {
        if (!textMesh)
            textMesh = GetComponent<TextMeshPro>();

        if (Camera.main)
            cameraTransform = Camera.main.transform;
    }

    public void Setup(int damageAmount, HitLocation hitLocation)
    {
        textMesh.SetText(damageAmount.ToString());

        switch (hitLocation)
        {
            case HitLocation.Core:
                textMesh.fontSize = 5f;
                textMesh.color = Color.white;
                transform.localScale = Vector3.one;
                break;
            case HitLocation.Head:
                textMesh.fontSize = 7f;
                textMesh.color = Color.red;
                transform.localScale = Vector3.one * 1.3f;
                break;
            case HitLocation.WeakPoint:
                textMesh.fontSize = 8f;
                textMesh.color = Color.yellow;
                transform.localScale = Vector3.one * 1.4f;
                break;
            case HitLocation.Limb:
                textMesh.fontSize = 4f;
                textMesh.color = Color.white;
                transform.localScale = Vector3.one;
                break;
            case HitLocation.ArmorPlating:
                textMesh.fontSize = 5f;
                textMesh.color = Color.gray;
                transform.localScale = Vector3.one;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hitLocation), hitLocation, null);
        }

        textColor = textMesh.color;
    }

    private void Update()
    {
        if (cameraTransform)
        {
            transform.rotation = cameraTransform.rotation;
        }

        transform.position += Vector3.up * (moveSpeedY * Time.deltaTime);

        disappearTimer -= Time.deltaTime;
        if (disappearTimer <= 0)
        {
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
}