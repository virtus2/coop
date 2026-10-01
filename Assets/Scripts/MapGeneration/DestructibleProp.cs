using UnityEngine;

namespace CoopGame.MapGeneration
{
    public class DestructibleProp : MonoBehaviour
    {
        [Header("Save / Load")]
        public string SaveId; // MapGenerator가 방 배치 시 자동으로 발급해주는 ID

        [Header("Prop Data")]
        public float health = 100f;
        public GameObject dropItemPrefab; // 부서졌을 때 튀어나올 고철/부품 프리팹

        /// <summary>
        /// 아이템이 타격을 입어 체력이 깎일 때 호출되는 함수
        /// </summary>
        public void TakeDamage(float damage)
        {
            health -= damage;
            if (health <= 0)
            {
                DestroyProp();
            }
        }

        private void DestroyProp()
        {
            // 1. 파괴되었음을 세이브 매니저에 등록
            // CoopGame.SaveSystem.AdvancedSaveLoadManager.Instance.RegisterDestroyedProp(SaveId);

            // 2. 자원 드롭
            if (dropItemPrefab != null)
            {
                Instantiate(dropItemPrefab, transform.position, Quaternion.identity);
            }

            // 3. 오브젝트 삭제
            Destroy(gameObject);
        }
    }
}
