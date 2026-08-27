using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class PlayerCheatService
    {
        public bool IsEnabled { get; set; } = true;

        public void Apply()
        {
            var player = Game.Player;
            var character = player.Character;
            if (character == null || !character.Exists()) return;

            if (!IsEnabled)
            {
                character.IsInvincible = false;
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, character.Handle, false);
                Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, character.Handle, true);
                character.IsBulletProof = false;
                character.IsExplosionProof = false;
                character.IsFireProof = false;
                character.IsMeleeProof = false;
                character.IsCollisionProof = false;
                return;
            }

            player.RefillSpecialAbility();
            character.Armor = ModSettings.MaxStat;
            character.Health = ModSettings.MaxStat;
            character.Accuracy = 100;
            character.ClearVisibleDamage();
            character.CanBeDraggedOutOfVehicle = false;
            character.KnockOffVehicleType = KnockOffVehicleType.Never;
            character.IsInvincible = true;
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, character.Handle, true);
            Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, character.Handle, false);
            character.IsExplosionProof = true;
            character.IsBulletProof = true;
            character.IsFireProof = true;
            character.IsMeleeProof = true;

            if (character.Weapons.Current != null)
            {
                character.Weapons.Current.InfiniteAmmo = true;
                character.Weapons.Current.InfiniteAmmoClip = true;
            }
        }

        public void Disable()
        {
            IsEnabled = false;
            var character = Game.Player.Character;
            if (character != null && character.Exists())
            {
                character.IsInvincible = false;
                Function.Call(Hash.SET_ENTITY_INVINCIBLE, character.Handle, false);
                Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, character.Handle, true);
                character.IsBulletProof = false;
                character.IsExplosionProof = false;
                character.IsFireProof = false;
                character.IsMeleeProof = false;
                character.IsCollisionProof = false;
            }
        }
    }
}
