using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class CollisionMatrixTests
    {
        [Test]
        public void AllCollide_ThenDisablePair_IsSymmetric()
        {
            var a = new AuraPhysicsLayer(0);
            var b = new AuraPhysicsLayer(1);
            var matrix = AuraCollisionMatrix.CreateAllCollide();

            Assert.IsTrue(matrix.CanCollide(a, b));

            matrix.SetCollision(a, b, false);
            Assert.IsFalse(matrix.CanCollide(a, b));
            Assert.IsFalse(matrix.CanCollide(b, a));

            matrix.SetCollision(a, b, true);
            Assert.IsTrue(matrix.CanCollide(a, b));
        }

        [Test]
        public void NoneCollide_ThenEnableSelf()
        {
            var layer = new AuraPhysicsLayer(3);
            var matrix = AuraCollisionMatrix.CreateNoneCollide();

            Assert.IsFalse(matrix.CanCollide(layer, layer));

            matrix.SetSelfCollision(layer, true);
            Assert.IsTrue(matrix.CanCollide(layer, layer));
        }

        [Test]
        public void LayerMask_FromLayers_AndOverlap()
        {
            var player = new AuraPhysicsLayer(0);
            var enemy = new AuraPhysicsLayer(1);
            var environment = new AuraPhysicsLayer(2);

            var mask = AuraPhysicsLayerMask.FromLayers(player, enemy);

            Assert.IsTrue(mask.Includes(player));
            Assert.IsTrue(mask.Includes(enemy));
            Assert.IsFalse(mask.Includes(environment));
            Assert.IsTrue(mask.Overlaps(AuraPhysicsLayerMask.FromLayer(enemy)));
            Assert.IsFalse(mask.Overlaps(AuraPhysicsLayerMask.FromLayer(environment)));
        }

        [Test]
        public void Layer_OutOfRange_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = new AuraPhysicsLayer(AuraPhysicsLayer.MaxLayers));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _ = new AuraPhysicsLayer(-1));
        }
    }
}
