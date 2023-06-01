using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dongle : MonoBehaviour
{
    public GameManager manager;
    public ParticleSystem effect;
    public int level;
    
    Rigidbody2D rigid;
    Animator anim;
    SpriteRenderer spriteRenderer;

    CircleCollider2D circleCollider;

    bool isDrag; // flag변수
    bool isMerge; // Merge는 병합이라는 뜻
    bool isAttach;
    float deadTime;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
    }

    void OnEnable()
    {
        anim.SetInteger("Level", level);
    }

    void Update()
    {
        // 드래그 상태일 때만 마우스 x축 따라가기
        if(isDrag)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float LeftBorder = -4.2f + transform.localScale.x / 2f;
            float rightBorder = 4.2f - transform.localScale.x / 2f;

            if (mousePos.x < LeftBorder) {
                mousePos.x = LeftBorder;
            }
            else if (mousePos.x > rightBorder){
                mousePos.x = rightBorder;
            }

            mousePos.y = 8; // 로 유지를 하겠다
            mousePos.z = 0;
            transform.position = Vector3.Lerp(transform.position, mousePos, 0.5f);
        }
    }
    public void Drag()
    {
        // 드래그 플래그 ON
        isDrag = true;
    }

    public void Drop()
    {
        // 드래그 플래그 off, 물리효과 on
        isDrag = false;
        rigid.simulated = true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        StartCoroutine("AttachRoutine");
    }

    IEnumerator AttachRoutine()
    {
        if (isAttach)
            yield break;

        isAttach = true;
        manager.PlaySfx(GameManager.Sfx.Attach);

        yield return new WaitForSeconds(0.2f);
        isAttach = false;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 충돌 상대편이 동글이면..
        if(collision.gameObject.tag == "Dongle"){ // 프리팹 Dongle에서 Tag확인
            Dongle other = collision.gameObject.GetComponent<Dongle>(); // 동글 가지고 옴
            // 조건 비교 (같은 레벨인가, 지금합쳐지는 중이 아닌가, 만렙이 아닌가)
            if(level == other.level && !isMerge && !other.isMerge && level < 7) // other -> 상대편동글하고 레벨이 같냐?
                {
                    // 나와 상대편 위치 가져오기
                    float meX = transform.position.x;
                    float meY = transform.position.y;
                    float otherX = other.transform.position.x;
                    float otherY = other.transform.position.y;

                    // 내가 위에 있거나 혹은 같은 높이지만 오른쪽에 있을 때
                    if(meY < otherY || (meY == otherY && meX > otherX)){
                        other.Hide(transform.position);
                        LevelUp();
                    }
                }
        }
    }
    void LevelUp()
    {
        // 잠금 ON
        isMerge = true; // 합쳐지는 순간은 누구도 방해못함!!
        
        // 물리 속도 초기화
        rigid.velocity = Vector2.zero;
        rigid.angularVelocity = 0; // 회전속도 멈춰

        StartCoroutine("LevelUpRoutine");
    }

    IEnumerator LevelUpRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        // 레벨업 애니메이션
        anim.SetInteger("Level", level+1);
        manager.PlaySfx(GameManager.Sfx.LevelUp);
        EffectPlay();

        yield return new WaitForSeconds(0.3f);
        // 레벨업
        level++;
        // 최대 레벨 갱신
        manager.maxLevel = Mathf.Max(manager.maxLevel, level);
        // 잠금 OFF
        isMerge = false; // 이제 레벨업 끝났으니 merge 풀어주기
    }

    public void Hide(Vector3 targetPos)
    {
        // 잠금 ON
        isMerge = true;

        // 물리 효과 OFF
        rigid.simulated = false;
        circleCollider.enabled = false;

        StartCoroutine("HideRoutine", targetPos);

        // 게임 오버일 때는 이펙트 실행
        if (targetPos == Vector3.up * 100){
            EffectPlay();
        }
    }

    IEnumerator HideRoutine(Vector3 targetPos)
    {
        int timeCount = 0;
        // 20 프레임 동안 레벨업 동글로 이동
        while(timeCount < 20) {
            timeCount++;
            // 상대가 있을 때
            if(targetPos != Vector3.up * 100) {
                transform.position = Vector3.Lerp(transform.position, targetPos, 0.5f);
            }
            // 게임 오버일 때
            else if(targetPos == Vector3.up * 100) {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 0.25f);
            }
            yield return null; 
        }
        
        // 점수 증가
        manager.score += (int)Mathf.Pow(2, level); // pow의 결과값은 float여서 강제형변환
        // 비활성화
        gameObject.SetActive(false);
        // 잠금 OFF
        isMerge = false;
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.tag == "Finish"){
            // 데드 타임 증가
            deadTime += Time.deltaTime;
            // 2초 지나면 색상 변경으로 경고
            if(deadTime > 2){
                spriteRenderer.color = new Color(0.9f, 0.2f, 0.2f);
            }
            // 3초 지나면 게임 오버
            if(deadTime > 3){
                manager.Result();
            }
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Finish"){
            // 데드타임 및 색상 초기화
            deadTime = 0;
            spriteRenderer.color = Color.white;
        }
    }

    void OnDisable()
    {
        // 동글 속성 초기화
        level = 0;
        deadTime = 0;
        // 동글 트랜스폼 초기화
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        // 동글 물리 초기화
        rigid.simulated = false;
        rigid.velocity = Vector2.zero;
        rigid.angularVelocity = 0;
        circleCollider.enabled = true;
    }

    void EffectPlay()
    {
        // 파티클 위치와 크기설정
        effect.transform.position = transform.position;
        effect.transform.localScale = transform.localScale;
        // 파티클 플레이
        effect.Play();
    }
}
