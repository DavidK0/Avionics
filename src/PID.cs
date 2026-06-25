namespace Avionics {
    public class PID {
        public float Kp;
        public float Ki;
        public float Kd;

        public float integral;
        public float lastError;

        public PID(float kp, float ki, float kd) {
            Kp = kp;
            Ki = ki;
            Kd = kd;
            Reset();
        }
        public void Reset() {
            integral = 0f;
            lastError = 0f;
        }
        public float Update(float error, float dt) {
            integral += error * dt;
            float derivative = (error - lastError) / dt;

            lastError = error;

            return Kp * error + Ki * integral + Kd * derivative;
        }
        public string GetDebugString() {
            return $"{Kp}, {Ki}, {Kd}, {integral}, {lastError}";
        }
    }
}
