namespace Razor.Core.Engine.UnitTests;

public class SecurityManagerSequenceTests
{
    [Fact]
    public void DecryptMessage_Succeeds_WithMonotonicallyIncreasingSequence()
    {
        var sender = new SecurityManager(null);
        var receiver = new SecurityManager(null);

        sender.SetCloudPublicKey(receiver.GetPublicKey());
        receiver.SetCloudPublicKey(sender.GetPublicKey());

        const string nonce = "test-nonce-sequence";
        sender.SetNonce(nonce);
        receiver.SetNonce(nonce);

        sender.DeriveSharedSecret();
        receiver.DeriveSharedSecret();

        var cipherText = sender.EncryptMessage("hello");

        var result = receiver.DecryptMessage(cipherText);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void DecryptMessage_ThrowsSecurityException_OnReplayedMessage()
    {
        var sender = new SecurityManager(null);
        var receiver = new SecurityManager(null);

        sender.SetCloudPublicKey(receiver.GetPublicKey());
        receiver.SetCloudPublicKey(sender.GetPublicKey());

        const string nonce = "test-nonce-replay";
        sender.SetNonce(nonce);
        receiver.SetNonce(nonce);

        sender.DeriveSharedSecret();
        receiver.DeriveSharedSecret();

        var cipherText = sender.EncryptMessage("hello");

        receiver.DecryptMessage(cipherText);

        Assert.Throws<SecurityException>(() => receiver.DecryptMessage(cipherText));
    }

    [Fact]
    public void DecryptMessage_ThrowsSecurityException_OnOutOfOrderMessage()
    {
        var sender = new SecurityManager(null);
        var receiver = new SecurityManager(null);

        sender.SetCloudPublicKey(receiver.GetPublicKey());
        receiver.SetCloudPublicKey(sender.GetPublicKey());

        const string nonce = "test-nonce-out-of-order";
        sender.SetNonce(nonce);
        receiver.SetNonce(nonce);

        sender.DeriveSharedSecret();
        receiver.DeriveSharedSecret();

        var firstCipher = sender.EncryptMessage("first");
        var secondCipher = sender.EncryptMessage("second");

        receiver.DecryptMessage(secondCipher);

        Assert.Throws<SecurityException>(() => receiver.DecryptMessage(firstCipher));
    }
}
