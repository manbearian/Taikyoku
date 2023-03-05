
static class MockGuid
{
    public static Guid NewGuid(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}");
}
