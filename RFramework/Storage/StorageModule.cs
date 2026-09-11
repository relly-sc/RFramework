using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>存档模块核心实现。</summary>
    internal sealed class StorageModule : RFrameworkModule, IStorageModule
    {
        private const byte FormatVersion = 1;
        private static readonly byte[] Magic = { (byte)'U', (byte)'R', (byte)'F', (byte)'S' };

        private readonly object gate = new object();
        private readonly Dictionary<string, SemaphoreSlim> slotLocks =
            new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        private CancellationTokenSource shutdown = new CancellationTokenSource();

        private IStorageHelper helper;
        private IStorageSerializer serializer;
        private IDataProtector dataProtector;

        internal override int Order => 25;

        public void SetHelper(IStorageHelper value)
        {
            helper = value ?? throw new RFrameworkException("Storage helper is invalid.");
        }

        public void SetSerializer(IStorageSerializer value)
        {
            serializer = value ?? throw new RFrameworkException("Storage serializer is invalid.");
        }

        public void SetDataProtector(IDataProtector value)
        {
            dataProtector = value;
        }

        public async Task<StorageResult> SaveAsync<T>(
            string slotName,
            T data,
            StorageOptions options = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (!TryValidate(slotName, options, out StorageOptions effective, out StorageResult failure))
            {
                return failure;
            }

            if (data == null)
            {
                return StorageResult.Failure(
                    slotName, StorageExceptionReason.InvalidArgument, "Storage data is invalid.");
            }

            SemaphoreSlim slotLock = GetSlotLock(slotName);
            using (CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(ct, shutdown.Token))
            {
                try
                {
                    await slotLock.WaitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return StorageResult.Failure(
                        slotName, StorageExceptionReason.Cancelled, "Storage save was cancelled.");
                }

                try
                {
                    byte[] serialized;
                    try
                    {
                        serialized = RequireSerializer().Serialize(data);
                    }
                    catch (Exception exception)
                    {
                        return Fail(slotName, StorageExceptionReason.SerializationFailed,
                            "Storage serialization failed.", exception);
                    }

                    byte[] envelope = CreateEnvelope(serialized, effective.Version);
                    Clear(serialized);
                    byte[] payload = envelope;
                    try
                    {
                        if (effective.CompressionMode == StorageCompressionMode.GZip)
                        {
                            payload = Compress(envelope);
                            Clear(envelope);
                        }

                        if (effective.ProtectionMode == StorageProtectionMode.EncryptedAndAuthenticated)
                        {
                            if (dataProtector == null)
                            {
                                return StorageResult.Failure(slotName,
                                    StorageExceptionReason.KeyUnavailable,
                                    "Storage protection is enabled but no data protector is installed.");
                            }

                            byte[] protectedPayload;
                            try
                            {
                                protectedPayload = dataProtector.Protect(
                                    payload,
                                    ProtectedDataPayloadKind.Storage,
                                    effective.KeyId,
                                    CreateAssociatedData(slotName, effective.CompressionMode));
                            }
                            catch (RFrameworkException exception)
                            {
                                return Fail(slotName, MapProtectionFailure(exception),
                                    "Storage protection failed.", exception);
                            }

                            Clear(payload);
                            payload = protectedPayload;
                        }

                        try
                        {
                            await RequireHelper().WriteAtomicAsync(
                                slotName, payload, effective.CreateBackup, linked.Token)
                                .ConfigureAwait(false);
                            return StorageResult.Success(slotName, effective.Version, payload.Length);
                        }
                        catch (OperationCanceledException)
                        {
                            return StorageResult.Failure(slotName,
                                StorageExceptionReason.Cancelled, "Storage save was cancelled.");
                        }
                        catch (Exception exception)
                        {
                            return Fail(slotName, StorageExceptionReason.IoFailure,
                                "Storage file commit failed.", exception);
                        }
                    }
                    finally
                    {
                        Clear(payload);
                    }
                }
                finally
                {
                    slotLock.Release();
                }
            }
        }

        public async Task<StorageLoadResult<T>> LoadAsync<T>(
            string slotName,
            StorageOptions options = null,
            IStorageMigration<T> migration = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (!TryValidate(slotName, options, out StorageOptions effective, out StorageResult failure))
            {
                return StorageLoadResult<T>.Failure(slotName, failure.Reason, failure.Message);
            }

            SemaphoreSlim slotLock = GetSlotLock(slotName);
            using (CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(ct, shutdown.Token))
            {
                try
                {
                    await slotLock.WaitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return StorageLoadResult<T>.Failure(slotName,
                        StorageExceptionReason.Cancelled, "Storage load was cancelled.");
                }

                try
                {
                    StorageLoadResult<T> primary = await LoadCoreAsync<T>(
                        slotName, false, effective, migration, linked.Token).ConfigureAwait(false);
                    if (primary.Succeeded || !effective.RecoverFromBackup
                        || primary.Reason == StorageExceptionReason.Cancelled
                        || primary.Reason == StorageExceptionReason.ProtectionModeMismatch
                        || primary.Reason == StorageExceptionReason.CompressionModeMismatch)
                    {
                        return primary;
                    }

                    StorageLoadResult<T> backup = await LoadCoreAsync<T>(
                        slotName, true, effective, migration, linked.Token).ConfigureAwait(false);
                    return backup.Succeeded ? backup : primary;
                }
                finally
                {
                    slotLock.Release();
                }
            }
        }

        public async Task<StorageResult> DeleteAsync(
            string slotName, CancellationToken ct = default(CancellationToken))
        {
            if (!IsValidSlotName(slotName))
            {
                return StorageResult.Failure(slotName,
                    StorageExceptionReason.InvalidArgument, "Storage slot name is invalid.");
            }

            SemaphoreSlim slotLock = GetSlotLock(slotName);
            using (CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(ct, shutdown.Token))
            {
                try
                {
                    await slotLock.WaitAsync(linked.Token).ConfigureAwait(false);
                    try
                    {
                        await RequireHelper().DeleteAsync(slotName, linked.Token).ConfigureAwait(false);
                        return StorageResult.Success(slotName, 0, 0L);
                    }
                    finally
                    {
                        slotLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    return StorageResult.Failure(slotName,
                        StorageExceptionReason.Cancelled, "Storage delete was cancelled.");
                }
                catch (Exception exception)
                {
                    return Fail(slotName, StorageExceptionReason.IoFailure,
                        "Storage delete failed.", exception);
                }
            }
        }

        public Task<bool> ExistsAsync(
            string slotName, CancellationToken ct = default(CancellationToken))
        {
            if (!IsValidSlotName(slotName))
            {
                throw new RFrameworkException("Storage slot name is invalid.");
            }

            return RequireHelper().ExistsAsync(slotName, ct);
        }

        public Task<IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(
            CancellationToken ct = default(CancellationToken))
        {
            return RequireHelper().GetSlotsAsync(ct);
        }

        internal override void Tick(float deltaTime, float unscaledDeltaTime)
        {
        }

        internal override void Stop()
        {
            shutdown.Cancel();
            helper = null;
            serializer = null;
            dataProtector = null;
        }

        private async Task<StorageLoadResult<T>> LoadCoreAsync<T>(
            string slotName,
            bool backup,
            StorageOptions options,
            IStorageMigration<T> migration,
            CancellationToken ct)
        {
            byte[] payload;
            try
            {
                payload = await RequireHelper().ReadAsync(slotName, backup, ct).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return StorageLoadResult<T>.Failure(slotName,
                    StorageExceptionReason.NotFound, "Storage slot was not found.");
            }
            catch (OperationCanceledException)
            {
                return StorageLoadResult<T>.Failure(slotName,
                    StorageExceptionReason.Cancelled, "Storage load was cancelled.");
            }
            catch (Exception exception)
            {
                return LoadFail<T>(slotName, StorageExceptionReason.IoFailure,
                    "Storage file read failed.", exception);
            }

            try
            {
                bool protectedPayload = ProtectedDataHeader.HasMagic(payload);
                if (options.ProtectionMode == StorageProtectionMode.None && protectedPayload)
                {
                    return StorageLoadResult<T>.Failure(slotName,
                        StorageExceptionReason.ProtectionModeMismatch,
                        "Storage data is encrypted, but protection is disabled for this load.");
                }

                if (options.ProtectionMode == StorageProtectionMode.EncryptedAndAuthenticated
                    && IsPlainStoragePayload(payload))
                {
                    return StorageLoadResult<T>.Failure(slotName,
                        StorageExceptionReason.ProtectionModeMismatch,
                        "Storage data is not encrypted, but protected loading was requested.");
                }

                if (!protectedPayload && CompressionModeDoesNotMatch(payload, options.CompressionMode))
                {
                    return StorageLoadResult<T>.Failure(slotName,
                        StorageExceptionReason.CompressionModeMismatch,
                        "Storage compression mode does not match the load options.");
                }

                if (options.ProtectionMode == StorageProtectionMode.EncryptedAndAuthenticated)
                {
                    if (dataProtector == null)
                    {
                        return StorageLoadResult<T>.Failure(slotName,
                            StorageExceptionReason.KeyUnavailable,
                            "Storage protection is enabled but no data protector is installed.");
                    }

                    try
                    {
                        byte[] plaintext = dataProtector.Unprotect(
                            payload,
                            ProtectedDataPayloadKind.Storage,
                            CreateAssociatedData(slotName, options.CompressionMode));
                        Clear(payload);
                        payload = plaintext;
                    }
                    catch (RFrameworkException exception)
                    {
                        StorageExceptionReason reason = MapProtectionFailure(exception);
                        if (reason == StorageExceptionReason.AuthenticationFailed
                            && CanUnprotectWithAlternateCompression(payload, slotName, options))
                        {
                            return StorageLoadResult<T>.Failure(slotName,
                                StorageExceptionReason.CompressionModeMismatch,
                                "Storage compression mode does not match the load options.");
                        }
                        return LoadFail<T>(slotName, reason,
                            "Storage authentication or decryption failed.", exception);
                    }
                }

                if (options.CompressionMode == StorageCompressionMode.GZip)
                {
                    try
                    {
                        byte[] decompressed = Decompress(payload);
                        Clear(payload);
                        payload = decompressed;
                    }
                    catch (Exception exception)
                    {
                        return LoadFail<T>(slotName, StorageExceptionReason.FormatInvalid,
                            "Storage compressed data is invalid.", exception);
                    }
                }

                if (!TryReadEnvelope(payload, out int storedVersion, out byte[] serialized))
                {
                    return StorageLoadResult<T>.Failure(slotName,
                        StorageExceptionReason.FormatInvalid,
                        "Storage format is invalid or unsupported.");
                }

                T data;
                try
                {
                    data = RequireSerializer().Deserialize<T>(serialized);
                }
                catch (Exception exception)
                {
                    return LoadFail<T>(slotName, StorageExceptionReason.SerializationFailed,
                        "Storage deserialization failed.", exception);
                }
                finally
                {
                    Clear(serialized);
                }

                if (storedVersion > options.Version)
                {
                    return StorageLoadResult<T>.Failure(slotName,
                        StorageExceptionReason.MigrationFailed,
                        "Storage version is newer than the current application supports.");
                }

                if (storedVersion < options.Version)
                {
                    if (migration == null)
                    {
                        return StorageLoadResult<T>.Failure(slotName,
                            StorageExceptionReason.MigrationFailed,
                            "Storage data requires a migration, but no migration is installed.");
                    }

                    try
                    {
                        data = migration.Migrate(data, storedVersion, options.Version);
                    }
                    catch (Exception exception)
                    {
                        return LoadFail<T>(slotName, StorageExceptionReason.MigrationFailed,
                            "Storage migration failed.", exception);
                    }
                }

                return StorageLoadResult<T>.Success(
                    slotName, data, options.Version, backup);
            }
            finally
            {
                Clear(payload);
            }
        }

        private static byte[] CreateEnvelope(byte[] serialized, int version)
        {
            if (serialized == null)
            {
                throw new RFrameworkException("Storage serializer returned null data.");
            }

            byte[] result = new byte[checked(13 + serialized.Length)];
            Buffer.BlockCopy(Magic, 0, result, 0, Magic.Length);
            result[4] = FormatVersion;
            WriteInt32(result, 5, version);
            WriteInt32(result, 9, serialized.Length);
            Buffer.BlockCopy(serialized, 0, result, 13, serialized.Length);
            return result;
        }

        private static bool IsPlainStoragePayload(byte[] payload)
        {
            if (payload == null || payload.Length < 2) return false;
            if (payload[0] == 0x1f && payload[1] == 0x8b) return true;
            if (payload.Length < Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (payload[i] != Magic[i]) return false;
            }
            return true;
        }

        private static bool CompressionModeDoesNotMatch(
            byte[] payload, StorageCompressionMode expected)
        {
            bool isGZip = payload != null && payload.Length >= 2
                && payload[0] == 0x1f && payload[1] == 0x8b;
            bool isEnvelope = IsPlainStoragePayload(payload) && !isGZip;
            return expected == StorageCompressionMode.GZip ? isEnvelope : isGZip;
        }

        private bool CanUnprotectWithAlternateCompression(
            byte[] payload, string slotName, StorageOptions options)
        {
            StorageCompressionMode alternate = options.CompressionMode == StorageCompressionMode.GZip
                ? StorageCompressionMode.None
                : StorageCompressionMode.GZip;
            byte[] plaintext = null;
            try
            {
                plaintext = dataProtector.Unprotect(
                    payload,
                    ProtectedDataPayloadKind.Storage,
                    CreateAssociatedData(slotName, alternate));
                return true;
            }
            catch (RFrameworkException)
            {
                return false;
            }
            finally
            {
                Clear(plaintext);
            }
        }

        private static bool TryReadEnvelope(byte[] data, out int version, out byte[] serialized)
        {
            version = 0;
            serialized = null;
            if (data == null || data.Length < 13 || data[4] != FormatVersion)
            {
                return false;
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i]) return false;
            }

            version = ReadInt32(data, 5);
            int length = ReadInt32(data, 9);
            if (version < 1 || length < 0 || length != data.Length - 13)
            {
                return false;
            }

            serialized = new byte[length];
            Buffer.BlockCopy(data, 13, serialized, 0, length);
            return true;
        }

        private static byte[] Compress(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    gzip.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        private static byte[] Decompress(byte[] data)
        {
            using (MemoryStream input = new MemoryStream(data, false))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        private static byte[] CreateAssociatedData(string slotName, StorageCompressionMode compression)
        {
            return Encoding.UTF8.GetBytes("UnityRFramework.Storage|" + slotName + "|" + (int)compression);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return buffer[offset] | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);
        }

        private bool TryValidate(
            string slotName,
            StorageOptions options,
            out StorageOptions effective,
            out StorageResult failure)
        {
            StorageOptions source = options ?? new StorageOptions();
            effective = new StorageOptions
            {
                Version = source.Version,
                ProtectionMode = source.ProtectionMode,
                KeyId = source.KeyId,
                CompressionMode = source.CompressionMode,
                CreateBackup = source.CreateBackup,
                RecoverFromBackup = source.RecoverFromBackup
            };
            failure = null;
            if (!IsValidSlotName(slotName))
            {
                failure = StorageResult.Failure(slotName,
                    StorageExceptionReason.InvalidArgument, "Storage slot name is invalid.");
                return false;
            }

            if (effective.Version < 1)
            {
                failure = StorageResult.Failure(slotName,
                    StorageExceptionReason.InvalidArgument, "Storage version must be greater than zero.");
                return false;
            }

            if (effective.ProtectionMode != StorageProtectionMode.None
                && effective.ProtectionMode != StorageProtectionMode.EncryptedAndAuthenticated)
            {
                failure = StorageResult.Failure(slotName,
                    StorageExceptionReason.InvalidArgument, "Storage protection mode is invalid.");
                return false;
            }

            if (effective.CompressionMode != StorageCompressionMode.None
                && effective.CompressionMode != StorageCompressionMode.GZip)
            {
                failure = StorageResult.Failure(slotName,
                    StorageExceptionReason.InvalidArgument, "Storage compression mode is invalid.");
                return false;
            }

            if (effective.ProtectionMode == StorageProtectionMode.EncryptedAndAuthenticated
                && string.IsNullOrWhiteSpace(effective.KeyId))
            {
                failure = StorageResult.Failure(slotName,
                    StorageExceptionReason.InvalidArgument, "Storage key identifier is invalid.");
                return false;
            }

            return true;
        }

        private static bool IsValidSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName) || slotName == "." || slotName == "..")
            {
                return false;
            }

            return slotName.IndexOf('/') < 0 && slotName.IndexOf('\\') < 0
                && slotName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private SemaphoreSlim GetSlotLock(string slotName)
        {
            lock (gate)
            {
                if (!slotLocks.TryGetValue(slotName, out SemaphoreSlim value))
                {
                    value = new SemaphoreSlim(1, 1);
                    slotLocks.Add(slotName, value);
                }
                return value;
            }
        }

        private IStorageHelper RequireHelper()
        {
            return helper ?? throw new RFrameworkException("No storage helper is installed.");
        }

        private IStorageSerializer RequireSerializer()
        {
            return serializer ?? throw new RFrameworkException("No storage serializer is installed.");
        }

        private static StorageExceptionReason MapProtectionFailure(RFrameworkException exception)
        {
            string message = exception.Message ?? string.Empty;
            if (message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return StorageExceptionReason.KeyUnavailable;
            }
            if (message.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return StorageExceptionReason.AuthenticationFailed;
            }
            return StorageExceptionReason.FormatInvalid;
        }

        private static StorageResult Fail(
            string slotName, StorageExceptionReason reason, string message, Exception exception)
        {
            return StorageResult.Failure(slotName, reason,
                message + " " + exception.GetType().Name + ": " + exception.Message);
        }

        private static StorageLoadResult<T> LoadFail<T>(
            string slotName, StorageExceptionReason reason, string message, Exception exception)
        {
            return StorageLoadResult<T>.Failure(slotName, reason,
                message + " " + exception.GetType().Name + ": " + exception.Message);
        }

        private static void Clear(byte[] data)
        {
            if (data != null) Array.Clear(data, 0, data.Length);
        }
    }
}
