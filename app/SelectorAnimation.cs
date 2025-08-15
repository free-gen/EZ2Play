using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EZ2Play.App
{
    public class SelectorAnimation
    {
        public double MovementSpeedSeconds = 1.5;
        public double PauseDurationSeconds = 2.0;
        public double InitialDelaySeconds = 0.5;
        
        private LinearGradientBrush gradientBrush;
        private LinearGradientBrush horizontalGradientBrush;
        private PointAnimationUsingKeyFrames animation;
        private PointAnimationUsingKeyFrames endPointAnimation;

        public SelectorAnimation()
        {
            InitializeGradient();
            InitializeHorizontalGradient();
            InitializeAnimation();
        }

        private void InitializeGradient()
        {
            gradientBrush = new LinearGradientBrush();
            gradientBrush.StartPoint = new Point(-0.5, 0);
            gradientBrush.EndPoint = new Point(0, 0);

            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(128, 255, 255, 255), 0.0));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 255, 255, 255), 0.5));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(128, 255, 255, 255), 1.0));
        }

        private void InitializeHorizontalGradient()
        {
            horizontalGradientBrush = new LinearGradientBrush();
            horizontalGradientBrush.StartPoint = new Point(-0.5, 0);
            horizontalGradientBrush.EndPoint = new Point(0, 0);

            // Более яркий градиент для горизонтального режима
            horizontalGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 200, 200, 200), 0.0));
            horizontalGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.5));
            horizontalGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 200, 200, 200), 1.0));
        }

        private void InitializeAnimation()
        {
            var movementDuration = TimeSpan.FromSeconds(MovementSpeedSeconds);
            var totalDuration = TimeSpan.FromSeconds(MovementSpeedSeconds + PauseDurationSeconds);

            animation = new PointAnimationUsingKeyFrames();
            animation.RepeatBehavior = RepeatBehavior.Forever;
            animation.AutoReverse = false;
            animation.BeginTime = TimeSpan.FromSeconds(InitialDelaySeconds);
            animation.AccelerationRatio = 0.0;
            animation.DecelerationRatio = 0.0;

            animation.KeyFrames.Add(new LinearPointKeyFrame(new Point(-0.5, 0), TimeSpan.Zero));
            animation.KeyFrames.Add(new LinearPointKeyFrame(new Point(1.5, 0), movementDuration));
            animation.KeyFrames.Add(new LinearPointKeyFrame(new Point(3.0, 0), totalDuration));
        }

        public LinearGradientBrush GetAnimatedBrush()
        {
            var movementDuration = TimeSpan.FromSeconds(MovementSpeedSeconds);
            var totalDuration = TimeSpan.FromSeconds(MovementSpeedSeconds + PauseDurationSeconds);

            endPointAnimation = new PointAnimationUsingKeyFrames();
            endPointAnimation.RepeatBehavior = RepeatBehavior.Forever;
            endPointAnimation.AutoReverse = false;
            endPointAnimation.BeginTime = TimeSpan.FromSeconds(InitialDelaySeconds);
            endPointAnimation.AccelerationRatio = 0.0;
            endPointAnimation.DecelerationRatio = 0.0;

            endPointAnimation.KeyFrames.Add(new LinearPointKeyFrame(new Point(0, 0), TimeSpan.Zero));
            endPointAnimation.KeyFrames.Add(new LinearPointKeyFrame(new Point(2.0, 0), movementDuration));
            endPointAnimation.KeyFrames.Add(new LinearPointKeyFrame(new Point(4.0, 0), totalDuration));

            gradientBrush.BeginAnimation(LinearGradientBrush.StartPointProperty, animation);
            gradientBrush.BeginAnimation(LinearGradientBrush.EndPointProperty, endPointAnimation);
            
            return gradientBrush;
        }

        public LinearGradientBrush GetHorizontalAnimatedBrush()
        {
            var movementDuration = TimeSpan.FromSeconds(MovementSpeedSeconds);
            var totalDuration = TimeSpan.FromSeconds(MovementSpeedSeconds + PauseDurationSeconds);

            var horizontalEndPointAnimation = new PointAnimationUsingKeyFrames();
            horizontalEndPointAnimation.RepeatBehavior = RepeatBehavior.Forever;
            horizontalEndPointAnimation.AutoReverse = false;
            horizontalEndPointAnimation.BeginTime = TimeSpan.FromSeconds(InitialDelaySeconds);
            horizontalEndPointAnimation.AccelerationRatio = 0.0;
            horizontalEndPointAnimation.DecelerationRatio = 0.0;

            horizontalEndPointAnimation.KeyFrames.Add(new LinearPointKeyFrame(new Point(0, 0), TimeSpan.Zero));
            horizontalEndPointAnimation.KeyFrames.Add(new LinearPointKeyFrame(new Point(2.0, 0), movementDuration));
            horizontalEndPointAnimation.KeyFrames.Add(new LinearPointKeyFrame(new Point(4.0, 0), totalDuration));

            horizontalGradientBrush.BeginAnimation(LinearGradientBrush.StartPointProperty, animation);
            horizontalGradientBrush.BeginAnimation(LinearGradientBrush.EndPointProperty, horizontalEndPointAnimation);
            
            return horizontalGradientBrush;
        }

        public void StopAnimation()
        {
            gradientBrush.BeginAnimation(LinearGradientBrush.StartPointProperty, null);
            gradientBrush.BeginAnimation(LinearGradientBrush.EndPointProperty, null);
        }
    }
} 