using System;
using BattleRunner.Core.Save;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class SaveMigratorTests
    {
        [Test]
        public void CurrentVersionProfile_PassesThroughUnchanged()
        {
            var profile = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion, Keys = 3 };
            var migrated = SaveMigrator.Migrate(profile);
            Assert.AreEqual(SaveMigrator.CurrentVersion, migrated.SchemaVersion);
            Assert.AreEqual(3, migrated.Keys);
        }

        [Test]
        public void V1Profile_MigratesToCurrent_WithHealedDefaults()
        {
            var v1 = new PlayerProfile
            {
                SchemaVersion = 1,
                Inventory = null, // simulates fields absent from old JSON
                Equipped = null,
                StatPoints = null,
                PityCounter = -5
            };

            var migrated = SaveMigrator.Migrate(v1);

            Assert.AreEqual(SaveMigrator.CurrentVersion, migrated.SchemaVersion);
            Assert.IsNotNull(migrated.Inventory);
            Assert.IsNotNull(migrated.Equipped);
            Assert.IsNotNull(migrated.StatPoints);
            Assert.AreEqual(0, migrated.PityCounter);
        }

        [Test]
        public void NewerSchemaThanBuild_Throws()
        {
            var future = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion + 1 };
            Assert.Throws<NotSupportedException>(() => SaveMigrator.Migrate(future));
        }

        [Test]
        public void Checksum_RoundTripsAndDetectsTampering()
        {
            const string payload = "{\"SchemaVersion\":2,\"SoftCurrency\":100}";
            string sum = Checksum.Compute(payload);
            Assert.IsTrue(Checksum.Verify(payload, sum));
            Assert.IsFalse(Checksum.Verify(payload.Replace("100", "999999"), sum));
        }

        [Test]
        public void Checksum_IsStableAcrossCalls()
        {
            Assert.AreEqual(Checksum.Compute("abc"), Checksum.Compute("abc"));
            Assert.AreNotEqual(Checksum.Compute("abc"), Checksum.Compute("abd"));
        }

        [Test]
        public void ProfileHelpers_EquipAndStatAccessorsWork()
        {
            var profile = new PlayerProfile();
            profile.SetEquipped(BattleRunner.Core.Loot.GearSlot.Weapon, "inst1");
            profile.SetEquipped(BattleRunner.Core.Loot.GearSlot.Weapon, "inst2");
            Assert.AreEqual("inst2", profile.GetEquipped(BattleRunner.Core.Loot.GearSlot.Weapon));
            Assert.IsNull(profile.GetEquipped(BattleRunner.Core.Loot.GearSlot.Relic));

            profile.AddStatPoint("damage");
            profile.AddStatPoint("damage");
            Assert.AreEqual(2, profile.GetStatPoints("damage"));
            Assert.AreEqual(0, profile.GetStatPoints("health"));
        }
    }
}
