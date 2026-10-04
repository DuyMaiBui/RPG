using System;

namespace AuraEngine.Core
{
    /* Deterministic coyote-time and jump-buffer helper for platformer characters.
       Feed it the fixed step delta, the grounded flag reported by the previous
       move and whether jump was pressed since the last step; Update returns true
       on the step the jump command should be issued. Pure value logic: no Unity,
       no clocks, so identical inputs always give identical jumps. */
    public struct AuraJumpAssist
    {
        private const float Tiny = 1e-6f;

        private float _coyoteWindow;
        private float _bufferWindow;
        private float _coyoteRemaining;
        private float _bufferRemaining;

        public AuraJumpAssist(float coyoteTime, float jumpBufferTime)
        {
            _coyoteWindow = Math.Max(0f, coyoteTime);
            _bufferWindow = Math.Max(0f, jumpBufferTime);
            _coyoteRemaining = 0f;
            _bufferRemaining = 0f;
        }

        public float CoyoteTime => _coyoteWindow;

        public float JumpBufferTime => _bufferWindow;

        /* Seconds left in which a jump is still allowed after leaving the ground. */
        public float CoyoteRemaining => _coyoteRemaining;

        /* Seconds left in which an earlier jump press is still remembered. */
        public float BufferRemaining => _bufferRemaining;

        public void Configure(float coyoteTime, float jumpBufferTime)
        {
            _coyoteWindow = Math.Max(0f, coyoteTime);
            _bufferWindow = Math.Max(0f, jumpBufferTime);
        }

        public void Reset()
        {
            _coyoteRemaining = 0f;
            _bufferRemaining = 0f;
        }

        public bool Update(float deltaTime, bool grounded, bool jumpPressed)
        {
            _coyoteRemaining = grounded ? Math.Max(_coyoteWindow, Tiny) : Math.Max(0f, _coyoteRemaining - deltaTime);
            _bufferRemaining = jumpPressed ? Math.Max(_bufferWindow, Tiny) : Math.Max(0f, _bufferRemaining - deltaTime);

            if (_coyoteRemaining <= 0f || _bufferRemaining <= 0f)
                return false;

            _coyoteRemaining = 0f;
            _bufferRemaining = 0f;
            return true;
        }
    }
}
