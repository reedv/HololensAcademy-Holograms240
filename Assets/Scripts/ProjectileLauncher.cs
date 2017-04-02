﻿using UnityEngine;
using Academy.HoloToolkit.Sharing;

/// <summary>
/// Allows for this player to shoot projectiles.
/// AppStateManager delegates all tap gestures to this component
/// once it has the stage transform.
/// </summary>
public class ProjectileLauncher : MonoBehaviour
{
    /// <summary>
    /// Keep track of the last shot time to throttle users' shots
    /// </summary>
    float LastShotTime = 0;

    /// <summary>
    /// Initialize cutommessage hooks and audio sources
    /// </summary>
    void Start()
    {
        CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ShootProjectile] = this.ProcessRemoteProjectile;

        // We will use the camera's audio source to play sounds whenever a projectile hits the user's avatar
        // or when the user hits another player's avatar with a projectile.
        if (Camera.main.gameObject.GetComponent<AudioSource>() == null)
        {
            // Add an AudioSource and spatialize it.
            AudioSource audio = Camera.main.gameObject.AddComponent<AudioSource>() as AudioSource;
            audio.playOnAwake = false;
            audio.spatialize = true;
            audio.spatialBlend = 1.0f;
            audio.rolloffMode = AudioRolloffMode.Custom;
        }
    }

    /// <summary>
    /// OnSelect activation sent by gesture manager.
    /// Fires a projectile
    /// </summary>
    void OnSelect()
    {
        // player must wait 1 sec. before firing again
        if (Time.realtimeSinceStartup - LastShotTime > 1)
        {
            LastShotTime = Time.realtimeSinceStartup;
            SpawnProjectile(0);
        }
    }

    /// <summary>
    /// Spawns a new projectile in the world if the user
    /// doesn't already have one and fires it, broadcasting this info to other players.
    /// </summary>
    void SpawnProjectile(long UserId)
    {
        // fire in gaze direction
        Vector3 ProjectilePosition = Camera.main.transform.position + Camera.main.transform.forward * 0.85f;
        Vector3 ProjectileDirection = Camera.main.transform.forward;

        ShootProjectile(ProjectilePosition,
                    ProjectileDirection, UserId);

        // broadcast info of this fired projectile to other players
        Transform anchor = ImportExportAnchorManager.Instance.gameObject.transform;
        CustomMessages.Instance.SendShootProjectile(anchor.InverseTransformPoint(ProjectilePosition), anchor.InverseTransformDirection(ProjectileDirection));
    }


    /// <summary>
    /// Adds a new projectile to the world and launches it.
    /// </summary>
    /// <param name="start">Position to shoot from</param>
    /// <param name="direction">Position to shoot toward</param>
    /// <param name="radius">Size of destruction when colliding.</param>
    void ShootProjectile(Vector3 start, Vector3 direction, long OwningUser)
    {
        // Need to know the index in the PlayerAvatarStore to grab for this projectile's behavior.
        int AvatarIndex = 0;

        // Special case ID 0 to mean the _local_ user.  
        if (OwningUser == 0)
        {
            AvatarIndex = LocalPlayerManager.Instance.AvatarIndex;
        }
        else
        {
            RemotePlayerManager.RemoteHeadInfo headInfo = RemotePlayerManager.Instance.GetRemoteHeadInfo(OwningUser);
            AvatarIndex = headInfo.PlayerAvatarIndex;
        }

        PlayerAvatarParameters ownerAvatarParameters = PlayerAvatarStore.Instance.PlayerAvatars[AvatarIndex].GetComponent<PlayerAvatarParameters>();

        // spawn projectile
        GameObject spawnedProjectile = (GameObject)Instantiate(ownerAvatarParameters.PlayerShotObject);
        spawnedProjectile.transform.position = start;

        // Set projectile color to be the same as the avatar color.
        FriendlyDrone drone = PlayerAvatarStore.Instance.PlayerAvatars[AvatarIndex].GetComponentInChildren<FriendlyDrone>();
        if (drone != null)
        {
            spawnedProjectile.GetComponentInChildren<Renderer>().materials[1].SetColor("_EmissionColor", drone.EmissiveColor);
            foreach(ParticleSystem particleSystem in spawnedProjectile.transform.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.startColor = drone.EmissiveColor;
            }
        }

        // fire projectile in direction
        ProjectileBehavior pc = spawnedProjectile.GetComponentInChildren<ProjectileBehavior>();
        pc.startDir = direction;
        pc.OwningUserId = OwningUser;
    }

    /// <summary>
    /// Process user hit.
    /// </summary>
    /// <param name="msg"></param>
    void ProcessRemoteProjectile(NetworkInMessage msg)
    {
        // Parse the message
        long userID = msg.ReadInt64();
        Vector3 remoteProjectilePosition = CustomMessages.Instance.ReadVector3(msg);

        Vector3 remoteProjectileDirection = CustomMessages.Instance.ReadVector3(msg);

        Transform anchor = ImportExportAnchorManager.Instance.gameObject.transform;
        ShootProjectile(anchor.TransformPoint(remoteProjectilePosition), anchor.TransformDirection(remoteProjectileDirection), userID);
    }
}
